using Bfs.Api.Config;
using Bfs.Api.Licencias;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations.Worms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Pesadas;

public record ResultadoPesaje(Pesada? Pesada, string? Error = null, bool NoEncontrada = false, bool Bloqueada = false)
{
    public static ResultadoPesaje Ok(Pesada p) => new(p);
    public static ResultadoPesaje Invalida(string error) => new(null, error);
    public static ResultadoPesaje Inexistente() => new(null, NoEncontrada: true);
    public static ResultadoPesaje Prohibida(string motivo) => new(null, motivo, Bloqueada: true);
}

/// <summary>Quién está pesando y si tiene el permiso de cargar el peso a mano.</summary>
public record Operador(string Email, bool PuedePesoManual);

/// <summary>
/// Reglas del pesaje: Normal (entrada → salida), Tara conocida (un paso), Tomar tara, aduana,
/// ingreso manual con clave, anulación y numeración de tickets por puesto.
/// </summary>
public class PesajeService
{
    private const int Reintentos = 3;

    private readonly BfsDbContext _db;
    private readonly IBalanzas _balanzas;
    private readonly ConfigService _config;
    private readonly LicenciaService _licencia;
    private readonly PasswordHasher<Usuario> _hasher;

    public PesajeService(BfsDbContext db, IBalanzas balanzas, ConfigService config, LicenciaService licencia, PasswordHasher<Usuario> hasher)
    {
        _db = db;
        _balanzas = balanzas;
        _config = config;
        _licencia = licencia;
        _hasher = hasher;
    }

    /// <param name="soloCliente">Si viene, solo las pesadas de ese cliente (usuarios con rol cliente).</param>
    public Task<List<Pesada>> ListarAsync(Guid? empresaId, Guid? soloCliente = null)
    {
        var q = _db.Pesadas.AsQueryable();
        if (empresaId.HasValue) q = q.Where(p => p.EmpresaId == empresaId);
        if (soloCliente.HasValue) q = q.Where(p => p.ClienteId == soloCliente);
        return q.OrderByDescending(p => p.EntradaEn).Take(200).ToListAsync();
    }

    /// <summary>Todas las que esperan salida, sin límite: son las que hay que poder retomar.</summary>
    public Task<List<Pesada>> PendientesAsync() =>
        _db.Pesadas.Where(p => p.Estado == EstadosPesada.Entrada).OrderBy(p => p.EntradaEn).ToListAsync();

    public Task<ResultadoPesaje> RegistrarEntradaAsync(EntradaRequest req, Operador op) => ConReintento(async () =>
    {
        // Solo se bloquean las entradas: la salida de un camión que ya entró siempre se puede cerrar.
        var licencia = _licencia.Estado();
        if (!licencia.PuedePesar) return ResultadoPesaje.Prohibida(licencia.Mensaje);

        var configs = await _config.GlobalAsync();
        var (pesada, error) = await NuevaPesada(req, op, configs);
        if (error != null) return error;

        var peso = TomarPeso(req.PesoEntrada, req.ClaveManual, req.BalanzaId, op, configs);
        if (peso.Error != null) return peso.Error;
        pesada!.PesoEntrada = peso.Kg;
        pesada.Balanza = peso.Balanza;
        pesada.EntradaManualPor = peso.Manual ? op.Email : null;

        _db.Pesadas.Add(pesada);
        await RecordarVehiculo(pesada);
        WormsOutbox.Encolar(_db, configs, pesada, "ENTRADA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    });

    public Task<ResultadoPesaje> RegistrarSalidaAsync(Guid id, SalidaRequest req, Operador op) => ConReintento(async () =>
    {
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == EstadosPesada.Salida) return ResultadoPesaje.Invalida("Ya tiene salida");
        if (pesada.Estado == EstadosPesada.Anulada) return ResultadoPesaje.Invalida("La pesada está anulada");

        var configs = await _config.GlobalAsync();
        var peso = TomarPeso(req.PesoSalida, req.ClaveManual, req.BalanzaId, op, configs);
        if (peso.Error != null) return peso.Error;

        pesada.PesoSalida = peso.Kg;
        pesada.BalanzaSalida = peso.Balanza;
        pesada.SalidaManualPor = peso.Manual ? op.Email : null;
        pesada.OperadorSalida = op.Email;
        pesada.TaraPallets = req.TaraPallets ?? pesada.TaraPallets;
        Cerrar(pesada);
        WormsOutbox.Encolar(_db, configs, pesada, "SALIDA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    });

    /// <summary>
    /// Tara conocida: el bruto se toma ahora y la tara es la guardada para esa patente o carro.
    /// Escribir otra tara a mano cuenta como ingreso manual.
    /// </summary>
    public Task<ResultadoPesaje> RegistrarConTaraConocidaAsync(TaraConocidaRequest req, Operador op) => ConReintento(async () =>
    {
        var licencia = _licencia.Estado();
        if (!licencia.PuedePesar) return ResultadoPesaje.Prohibida(licencia.Mensaje);

        var configs = await _config.GlobalAsync();
        if (!Si(configs, ConfigKeys.TaraConocida)) return ResultadoPesaje.Prohibida("La tara conocida no está habilitada");

        var (pesada, error) = await NuevaPesada(req.Datos, op, configs);
        if (error != null) return error;

        var guardada = await _db.Taras.AsNoTracking().FirstOrDefaultAsync(t => t.Identificador == pesada!.PatenteChasis);
        int tara;
        var taraManual = false;
        if (req.TaraManual is { } escrita && escrita != guardada?.Kilogramos)
        {
            if (ValidarManual(req.ClaveManual, op, configs) is { } errorManual) return errorManual;
            if (escrita <= 0) return ResultadoPesaje.Invalida("La tara tiene que ser mayor a cero");
            tara = escrita;
            taraManual = true;
        }
        else if (guardada != null)
        {
            tara = guardada.Kilogramos;
        }
        else
        {
            return ResultadoPesaje.Invalida($"No hay tara guardada para {pesada!.PatenteChasis}");
        }

        var bruto = TomarPeso(req.Datos.PesoEntrada, req.ClaveManual, req.Datos.BalanzaId, op, configs);
        if (bruto.Error != null) return bruto.Error;

        pesada!.Modo = ModosPesaje.TaraConocida;
        pesada.PesoEntrada = bruto.Kg;
        pesada.Balanza = bruto.Balanza;
        pesada.EntradaManualPor = bruto.Manual ? op.Email : null;
        pesada.PesoSalida = tara;
        pesada.BalanzaSalida = null;
        pesada.SalidaManualPor = taraManual ? op.Email : null;
        pesada.OperadorSalida = op.Email;
        Cerrar(pesada);

        _db.Pesadas.Add(pesada);
        await RecordarVehiculo(pesada);
        WormsOutbox.Encolar(_db, configs, pesada, "ENTRADA");
        WormsOutbox.Encolar(_db, configs, pesada, "SALIDA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    });

    /// <summary>Pesa el vehículo vacío y guarda (o actualiza) su tara para usarla después en "Tara conocida".</summary>
    public async Task<(TaraConocida? Tara, ResultadoPesaje? Error)> TomarTaraAsync(TomarTaraRequest req, Operador op)
    {
        var configs = await _config.GlobalAsync();
        if (!Si(configs, ConfigKeys.TaraConocida)) return (null, ResultadoPesaje.Prohibida("La tara conocida no está habilitada"));
        var id = Patente.Normalizar(req.Identificador);
        if (id.Length == 0) return (null, ResultadoPesaje.Invalida("Falta la patente o el número de carro"));

        var peso = TomarPeso(req.PesoManual, req.ClaveManual, req.BalanzaId, op, configs);
        if (peso.Error != null) return (null, peso.Error);

        var tara = await _db.Taras.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Identificador == id);
        if (tara == null)
        {
            tara = new TaraConocida { Id = Guid.NewGuid(), Identificador = id };
            _db.Taras.Add(tara);
        }
        tara.Eliminado = false;
        tara.Kilogramos = peso.Kg;
        tara.TomadaEn = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(req.Descripcion)) tara.Descripcion = req.Descripcion.Trim();
        await _db.SaveChangesAsync();
        return (tara, null);
    }

    /// <summary>
    /// A diferencia de la 3.x (que borraba la pendiente), la pesada queda marcada con quién, cuándo y por qué:
    /// el número de ticket no se reutiliza y la anulación se ve en los reportes.
    /// </summary>
    public async Task<ResultadoPesaje> AnularAsync(Guid id, string? motivo, Operador op)
    {
        if (string.IsNullOrWhiteSpace(motivo)) return ResultadoPesaje.Invalida("Indicá el motivo de la anulación");
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == EstadosPesada.Anulada) return ResultadoPesaje.Invalida("Ya estaba anulada");

        pesada.Estado = EstadosPesada.Anulada;
        pesada.AnuladaEn = DateTimeOffset.UtcNow;
        pesada.AnuladaPor = op.Email;
        pesada.MotivoAnulacion = motivo.Trim();
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    }

    /// <summary>Arma la pesada con los datos del camión y le asigna número de ticket. Todavía sin pesos.</summary>
    private async Task<(Pesada? Pesada, ResultadoPesaje? Error)> NuevaPesada(EntradaRequest req, Operador op, Dictionary<string, string> configs)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == req.EmpresaId);
        if (empresa == null) return (null, ResultadoPesaje.Invalida("Empresa inexistente"));
        var patente = Patente.Normalizar(req.PatenteChasis);
        if (patente.Length == 0) return (null, ResultadoPesaje.Invalida("Falta la patente"));

        var clienteId = await ResolverCliente(req.ClienteId, req.Cliente);
        if (req.ClienteId.HasValue && clienteId == null) return (null, ResultadoPesaje.Invalida("Cliente inexistente"));

        var tipoOperacion = string.IsNullOrWhiteSpace(req.TipoOperacion) ? TiposOperacion.Normal : req.TipoOperacion;
        DatosAduana? aduana = null;
        if (tipoOperacion == TiposOperacion.Aduana)
        {
            if (!Si(configs, ConfigKeys.Aduana)) return (null, ResultadoPesaje.Prohibida("Las operaciones de aduana no están habilitadas"));
            var (datos, errorAduana) = ArmarAduana(req.Aduana);
            if (errorAduana != null) return (null, ResultadoPesaje.Invalida(errorAduana));
            aduana = datos;
        }
        else if (tipoOperacion != TiposOperacion.Normal)
        {
            return (null, ResultadoPesaje.Invalida("Tipo de operación inválido"));
        }

        var puesto = int.TryParse(configs.GetValueOrDefault(ConfigKeys.Puesto), out var p) && p is >= 1 and <= 99 ? p : 1;
        var numero = await ProximoTicket(empresa, puesto);

        return (new Pesada
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresa.Id,
            TicketNumero = Tickets.Formatear(puesto, numero),
            Tipo = string.IsNullOrWhiteSpace(req.Tipo) ? configs.GetValueOrDefault(ConfigKeys.WormsTipoDefault, "INGRESO") : req.Tipo,
            Estado = EstadosPesada.Entrada,
            Modo = ModosPesaje.Normal,
            TipoOperacion = tipoOperacion,
            Aduana = aduana,
            PatenteChasis = patente,
            PatenteAcoplado = string.IsNullOrWhiteSpace(req.PatenteAcoplado) ? null : Patente.Normalizar(req.PatenteAcoplado),
            Conductor = Vacio(req.Conductor),
            Documento = Vacio(req.Documento),
            Cliente = Vacio(req.Cliente),
            ClienteId = clienteId,
            Transporte = Vacio(req.Transporte),
            Producto = Vacio(req.Producto),
            Procedencia = Vacio(req.Procedencia),
            Destino = Vacio(req.Destino),
            Comprobante = Vacio(req.Comprobante),
            Observaciones = Vacio(req.Observaciones),
            Operador = op.Email,
            TaraPallets = Math.Max(0, req.TaraPallets),
            EntradaEn = DateTimeOffset.UtcNow
        }, null);
    }

    private static (DatosAduana? Datos, string? Error) ArmarAduana(DatosAduanaDto? dto)
    {
        if (dto == null || !EscenariosAduana.Todos.Contains(dto.Escenario))
        {
            return (null, "Elegí el escenario de aduana: " + string.Join(" o ", EscenariosAduana.Todos));
        }
        if (dto.ContenedorTara < 0) return (null, "La tara del contenedor no puede ser negativa");
        var (cuit, errorCuit) = Cuit.Normalizar(dto.ExportadorCuit);
        if (errorCuit != null) return (null, "Exportador: " + errorCuit);

        return (new DatosAduana
        {
            Escenario = dto.Escenario,
            Exportador = Vacio(dto.Exportador),
            ExportadorCuit = cuit,
            OperacionId = Vacio(dto.OperacionId),
            ChoferTipoDocumento = Vacio(dto.ChoferTipoDocumento),
            ChoferNacionalidad = Vacio(dto.ChoferNacionalidad),
            ContenedorId = Vacio(dto.ContenedorId)?.ToUpperInvariant(),
            ContenedorTara = dto.ContenedorTara,
            BultoId = Vacio(dto.BultoId),
            BultoBruto = dto.BultoBruto,
            BultoTara = dto.BultoTara
        }, null);
    }

    private static void Cerrar(Pesada p)
    {
        p.SalidaEn = DateTimeOffset.UtcNow;
        p.Estado = EstadosPesada.Salida;
        var calc = NetoCalculator.Calcular(p.PesoEntrada, p.PesoSalida!.Value, p.TaraPallets);
        p.Bruto = calc.Bruto;
        p.Tara = calc.Tara;
        p.Neto = calc.Neto;
        if (p.Aduana != null && EscenariosAduana.Contenedor(p.Aduana.Escenario, calc.Neto, p.Aduana.ContenedorTara) is var (bruto, neto))
        {
            p.Aduana.ContenedorBruto = bruto;
            p.Aduana.ContenedorNeto = neto;
        }
    }

    private readonly record struct Peso(int Kg, string? Balanza, bool Manual, ResultadoPesaje? Error);

    /// <summary>
    /// El peso escrito a mano necesita el permiso, que el ingreso manual esté habilitado y la clave si hay una.
    /// El de la balanza, que esté leyendo y (si BFS lo exige) estable.
    /// </summary>
    private Peso TomarPeso(int? manual, string? clave, Guid? balanzaId, Operador op, Dictionary<string, string> configs)
    {
        if (manual is { } kg)
        {
            if (ValidarManual(clave, op, configs) is { } error) return new Peso(0, null, true, error);
            return kg > 0 ? new Peso(kg, null, true, null) : new Peso(0, null, true, ResultadoPesaje.Invalida("El peso tiene que ser mayor a cero"));
        }

        var l = _balanzas.Lectura(balanzaId);
        var errorLectura = l.Estado switch
        {
            "abierta" or "simulada" => null,
            "sin balanza" => "No hay ninguna balanza configurada",
            _ => $"La balanza {l.Nombre} no está leyendo ({l.Estado}{(l.Error is null ? "" : ": " + l.Error)})"
        };
        if (errorLectura == null && !l.Estable && Si(configs, ConfigKeys.ExigirEstable, "true"))
        {
            errorLectura = "El peso todavía no está estable";
        }
        if (errorLectura == null && l.Kilogramos <= 0) errorLectura = $"La balanza {l.Nombre} marca {l.Kilogramos} kg";
        return errorLectura == null
            ? new Peso(l.Kilogramos, l.Nombre, false, null)
            : new Peso(0, l.Nombre, false, ResultadoPesaje.Invalida(errorLectura));
    }

    private ResultadoPesaje? ValidarManual(string? clave, Operador op, Dictionary<string, string> configs)
    {
        if (!op.PuedePesoManual) return ResultadoPesaje.Prohibida("No tenés permiso para cargar el peso a mano");
        if (!Si(configs, ConfigKeys.IngresoManual, "true")) return ResultadoPesaje.Prohibida("El ingreso manual de peso está deshabilitado");

        var hash = configs.GetValueOrDefault(ConfigKeys.ClaveManualHash);
        if (string.IsNullOrEmpty(hash)) return null;
        if (string.IsNullOrEmpty(clave) || _hasher.VerifyHashedPassword(new Usuario(), hash, clave) == PasswordVerificationResult.Failed)
        {
            return ResultadoPesaje.Prohibida("Clave de ingreso manual incorrecta");
        }
        return null;
    }

    private async Task<int> ProximoTicket(Empresa empresa, int puesto)
    {
        var contador = await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == empresa.Id && c.Puesto == puesto);
        if (contador == null)
        {
            // El puesto 1 sigue la numeración que traía la empresa (spike / importación de la 3.x).
            contador = new ContadorTicket { Id = Guid.NewGuid(), EmpresaId = empresa.Id, Puesto = puesto, Proximo = puesto == 1 ? Math.Max(1, empresa.ProximoTicket) : 1 };
            _db.ContadoresTicket.Add(contador);
        }
        return contador.Proximo++;
    }

    /// <summary>
    /// Si dos pesadas toman el mismo número a la vez, una falla por la versión del contador (o el índice único del ticket):
    /// se descarta lo hecho y se vuelve a intentar con el número siguiente.
    /// </summary>
    private async Task<ResultadoPesaje> ConReintento(Func<Task<ResultadoPesaje>> operacion)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await operacion();
            }
            catch (DbUpdateException) when (intento < Reintentos)
            {
                _db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Guarda los datos de esta patente para autocompletar la próxima vez. Lo vacío no pisa lo que había.</summary>
    private async Task RecordarVehiculo(Pesada p)
    {
        var datos = await _db.DatosVehiculos.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.PatenteChasis == p.PatenteChasis);
        if (datos == null)
        {
            datos = new DatosVehiculo { Id = Guid.NewGuid(), PatenteChasis = p.PatenteChasis };
            _db.DatosVehiculos.Add(datos);
        }
        datos.Eliminado = false;
        datos.PatenteAcoplado = p.PatenteAcoplado ?? datos.PatenteAcoplado;
        datos.Conductor = p.Conductor ?? datos.Conductor;
        datos.Documento = p.Documento ?? datos.Documento;
        datos.Transporte = p.Transporte ?? datos.Transporte;
        datos.TipoDocumento = p.Aduana?.ChoferTipoDocumento ?? datos.TipoDocumento;
        datos.Nacionalidad = p.Aduana?.ChoferNacionalidad ?? datos.Nacionalidad;
    }

    private static bool Si(Dictionary<string, string> c, string clave, string porDefecto = "false") =>
        c.GetValueOrDefault(clave, porDefecto) == "true";

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Por id, o por razón social exacta (sin distinguir mayúsculas) si el operador la escribió a mano.</summary>
    private async Task<Guid?> ResolverCliente(Guid? id, string? razonSocial)
    {
        if (id.HasValue)
        {
            return await _db.Clientes.AnyAsync(c => c.Id == id) ? id : null;
        }
        if (string.IsNullOrWhiteSpace(razonSocial)) return null;

        var buscado = razonSocial.Trim().ToLower();
        var ids = await _db.Clientes.Where(c => c.RazonSocial.ToLower() == buscado).Select(c => c.Id).Take(2).ToListAsync();
        return ids.Count == 1 ? ids[0] : null;
    }
}
