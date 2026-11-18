using System.Text.Json;
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
    private const int Reintentos = 8;

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
        await EncolarWorms(configs, pesada, WormsEventos.Entrada);
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
        await EncolarWorms(configs, pesada, WormsEventos.Salida);
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
        await EncolarWorms(configs, pesada, WormsEventos.Entrada, WormsEventos.Salida);
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

    /// <summary>Campos que se pueden corregir: clave (como en el formulario), nombre para el historial, lectura y escritura.</summary>
    private static readonly (string Clave, string Nombre, Func<Pesada, string?> Leer, Action<Pesada, string?> Escribir)[] Corregibles =
    [
        ("tipo", "Tipo", p => p.Tipo, (p, v) => p.Tipo = v!),
        ("patenteChasis", "Patente chasis", p => p.PatenteChasis, (p, v) => p.PatenteChasis = v!),
        ("patenteAcoplado", "Patente acoplado", p => p.PatenteAcoplado, (p, v) => p.PatenteAcoplado = v),
        ("conductor", "Conductor", p => p.Conductor, (p, v) => p.Conductor = v),
        ("documento", "Documento", p => p.Documento, (p, v) => p.Documento = v),
        ("tipoDocumento", "Tipo de documento", p => p.TipoDocumento, (p, v) => p.TipoDocumento = v),
        ("nacionalidad", "Nacionalidad", p => p.Nacionalidad, (p, v) => p.Nacionalidad = v),
        ("cliente", "Cliente", p => p.Cliente, (p, v) => p.Cliente = v),
        ("transporte", "Transporte", p => p.Transporte, (p, v) => p.Transporte = v),
        ("producto", "Producto", p => p.Producto, (p, v) => p.Producto = v),
        ("procedencia", "Procedencia", p => p.Procedencia, (p, v) => p.Procedencia = v),
        ("destino", "Destino", p => p.Destino, (p, v) => p.Destino = v),
        ("comprobante", "Remito", p => p.Comprobante, (p, v) => p.Comprobante = v),
        ("observaciones", "Observaciones", p => p.Observaciones, (p, v) => p.Observaciones = v),
        ("aduana.exportador", "Exportador", p => p.Aduana?.Exportador, (p, v) => p.Aduana!.Exportador = v),
        ("aduana.exportadorCuit", "CUIT exportador", p => p.Aduana?.ExportadorCuit, (p, v) => p.Aduana!.ExportadorCuit = v),
        ("aduana.ata", "ATA", p => p.Aduana?.Ata, (p, v) => p.Aduana!.Ata = v),
        ("aduana.ataCuit", "CUIT ATA", p => p.Aduana?.AtaCuit, (p, v) => p.Aduana!.AtaCuit = v),
        ("aduana.operacionId", "N° de operación", p => p.Aduana?.OperacionId, (p, v) => p.Aduana!.OperacionId = v),
        ("aduana.destinacionAduanera", "Destinación aduanera", p => p.Aduana?.DestinacionAduanera, (p, v) => p.Aduana!.DestinacionAduanera = v),
        ("aduana.contenedorId", "Contenedor", p => p.Aduana?.ContenedorId, (p, v) => p.Aduana!.ContenedorId = v?.ToUpperInvariant()),
        ("aduana.bultoId", "Bulto", p => p.Aduana?.BultoId, (p, v) => p.Aduana!.BultoId = v)
    ];

    /// <summary>
    /// Corrige datos de una pesada sin tocar la base a mano: pide permiso, motivo y (si hay) la clave de corrección,
    /// y deja en el historial quién cambió qué. Los pesos, fechas, empresa y número de ticket no se corrigen.
    /// </summary>
    public async Task<ResultadoPesaje> CorregirAsync(Guid id, CorreccionRequest req, string usuario)
    {
        var configs = await _config.GlobalAsync();
        var hash = configs.GetValueOrDefault(ConfigKeys.ClaveCorreccionHash);
        if (!string.IsNullOrEmpty(hash) && (string.IsNullOrEmpty(req.Clave)
                || _hasher.VerifyHashedPassword(new Usuario(), hash, req.Clave) == PasswordVerificationResult.Failed))
        {
            return ResultadoPesaje.Prohibida("Clave de corrección incorrecta");
        }
        if (string.IsNullOrWhiteSpace(req.Motivo)) return ResultadoPesaje.Invalida("Indicá el motivo de la corrección");

        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == EstadosPesada.Anulada) return ResultadoPesaje.Invalida("La pesada está anulada");

        var cambios = new List<CambioCampo>();
        foreach (var (clave, valor) in req.Datos ?? [])
        {
            var campo = Corregibles.FirstOrDefault(c => c.Clave == clave);
            if (campo.Clave == null) return ResultadoPesaje.Invalida($"El campo {clave} no se puede corregir");
            if (clave.StartsWith("aduana.", StringComparison.Ordinal) && pesada.Aduana == null)
            {
                return ResultadoPesaje.Invalida("La pesada no es una operación de aduana");
            }

            var (nuevo, error) = NormalizarCorreccion(clave, valor);
            if (error != null) return ResultadoPesaje.Invalida(error);
            var anterior = campo.Leer(pesada);
            if (anterior == nuevo) continue;
            campo.Escribir(pesada, nuevo);
            cambios.Add(new CambioCampo(campo.Nombre, anterior, nuevo));
        }
        if (cambios.Count == 0) return ResultadoPesaje.Invalida("No hay cambios");

        var faltan = FieldCatalog.Parse(configs.GetValueOrDefault(ConfigKeys.FieldsJson))
            .Where(f => f.Visible && f.Required && Corregibles.Any(c => c.Clave == f.Key)
                && string.IsNullOrWhiteSpace(Corregibles.First(c => c.Clave == f.Key).Leer(pesada)))
            .Select(f => f.Label)
            .ToList();
        if (faltan.Count > 0) return ResultadoPesaje.Invalida("No se puede dejar vacío: " + string.Join(", ", faltan));

        if (req.Datos!.ContainsKey("cliente")) pesada.ClienteId = await ResolverCliente(null, pesada.Cliente);

        var historial = JsonSerializer.Deserialize<List<CorreccionPesada>>(pesada.CorreccionesJson, JsonWeb) ?? [];
        historial.Add(new CorreccionPesada(DateTimeOffset.UtcNow, usuario, req.Motivo.Trim(), cambios));
        pesada.CorreccionesJson = JsonSerializer.Serialize(historial, JsonWeb);

        // WORMS guarda la última versión de cada evento: se reenvían los que ya salieron (si BFS lo habilitó).
        if (Si(configs, ConfigKeys.WormsReenviarCorrecciones))
        {
            await EncolarWorms(configs, pesada, pesada.Estado == EstadosPesada.Salida
                ? [WormsEventos.Entrada, WormsEventos.Salida]
                : [WormsEventos.Entrada]);
        }
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    }

    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private static (string? Valor, string? Error) NormalizarCorreccion(string clave, string? valor)
    {
        var v = Vacio(valor);
        switch (clave)
        {
            case "tipo":
                return v is "INGRESO" or "EGRESO" ? (v, null) : (null, "El tipo es INGRESO o EGRESO");
            case "patenteChasis":
                var patente = Patente.Normalizar(v ?? "");
                return patente.Length == 0 ? (null, "Falta la patente") : (patente, null);
            case "patenteAcoplado":
                return (v == null ? null : Patente.Normalizar(v), null);
            case "aduana.exportadorCuit":
            case "aduana.ataCuit":
                var (cuit, error) = Cuit.Normalizar(v);
                return error != null ? (null, error) : (cuit, null);
            default:
                return (v, null);
        }
    }

    /// <summary>Arma la pesada con los datos del camión y le asigna número de ticket. Todavía sin pesos.</summary>
    private async Task<(Pesada? Pesada, ResultadoPesaje? Error)> NuevaPesada(EntradaRequest req, Operador op, Dictionary<string, string> configs)
    {
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == req.EmpresaId);
        if (empresa == null) return (null, ResultadoPesaje.Invalida("Empresa inexistente"));
        var patente = Patente.Normalizar(req.PatenteChasis);
        if (patente.Length == 0) return (null, ResultadoPesaje.Invalida("Falta la patente"));

        var faltan = FieldCatalog.Parse(configs.GetValueOrDefault(ConfigKeys.FieldsJson))
            .Where(f => f.Visible && f.Required && CampoEntrada(req, f.Key) is { } v && string.IsNullOrWhiteSpace(v))
            .Select(f => f.Label)
            .ToList();
        if (faltan.Count > 0) return (null, ResultadoPesaje.Invalida("Completá: " + string.Join(", ", faltan)));

        var clienteId = await ResolverCliente(req.ClienteId, req.Cliente);
        if (req.ClienteId.HasValue && clienteId == null) return (null, ResultadoPesaje.Invalida("Cliente inexistente"));

        var tipoOperacion = string.IsNullOrWhiteSpace(req.TipoOperacion) ? TiposOperacion.Normal : req.TipoOperacion;
        DatosAduana? aduana = null;
        if (tipoOperacion == TiposOperacion.Aduana)
        {
            if (!Si(configs, ConfigKeys.Aduana)) return (null, ResultadoPesaje.Prohibida("Las operaciones de aduana no están habilitadas"));
            var (datos, errorAduana) = ArmarAduana(req.Aduana, req, configs);
            if (errorAduana != null) return (null, ResultadoPesaje.Invalida(errorAduana));
            aduana = datos;
        }
        else if (tipoOperacion != TiposOperacion.Normal)
        {
            return (null, ResultadoPesaje.Invalida("Tipo de operación inválido"));
        }

        var puesto = int.TryParse(configs.GetValueOrDefault(ConfigKeys.Puesto), out var p) && p is >= 1 and <= 99 ? p : 1;
        var soloNumero = Si(configs, ConfigKeys.TicketSoloNumero);
        var ticket = await ProximoTicket(empresa, puesto, soloNumero);

        return (new Pesada
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresa.Id,
            TicketNumero = ticket,
            Tipo = string.IsNullOrWhiteSpace(req.Tipo) ? configs.GetValueOrDefault(ConfigKeys.WormsTipoDefault, "INGRESO") : req.Tipo,
            Estado = EstadosPesada.Entrada,
            Modo = ModosPesaje.Normal,
            TipoOperacion = tipoOperacion,
            Aduana = aduana,
            PatenteChasis = patente,
            PatenteAcoplado = string.IsNullOrWhiteSpace(req.PatenteAcoplado) ? null : Patente.Normalizar(req.PatenteAcoplado),
            Conductor = Vacio(req.Conductor),
            Documento = Vacio(req.Documento),
            TipoDocumento = Vacio(req.TipoDocumento) ?? aduana?.ChoferTipoDocumento,
            Nacionalidad = Vacio(req.Nacionalidad) ?? aduana?.ChoferNacionalidad,
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

    /// <summary>El valor de un campo del formulario en el pedido; null si el campo no es de texto (no se valida acá).</summary>
    private static string? CampoEntrada(EntradaRequest r, string clave) => clave switch
    {
        "patenteChasis" => r.PatenteChasis ?? "",
        "patenteAcoplado" => r.PatenteAcoplado ?? "",
        "conductor" => r.Conductor ?? "",
        "documento" => r.Documento ?? "",
        "tipoDocumento" => r.TipoDocumento ?? r.Aduana?.ChoferTipoDocumento ?? "",
        "nacionalidad" => r.Nacionalidad ?? r.Aduana?.ChoferNacionalidad ?? "",
        "cliente" => r.Cliente ?? "",
        "transporte" => r.Transporte ?? "",
        "producto" => r.Producto ?? "",
        "procedencia" => r.Procedencia ?? "",
        "destino" => r.Destino ?? "",
        "comprobante" => r.Comprobante ?? "",
        "observaciones" => r.Observaciones ?? "",
        _ => null
    };

    private static (DatosAduana? Datos, string? Error) ArmarAduana(DatosAduanaDto? dto, EntradaRequest req, Dictionary<string, string> configs)
    {
        // BFS puede dejar fijo el escenario (ej. un cliente que nunca ingresa con contenedor).
        if (configs.GetValueOrDefault(ConfigKeys.AduanaEscenario) is { Length: > 0 } fijo && EscenariosAduana.Todos.Contains(fijo))
        {
            dto = (dto ?? new DatosAduanaDto(fijo)) with { Escenario = fijo };
        }
        if (dto == null || !EscenariosAduana.Todos.Contains(dto.Escenario))
        {
            return (null, "Elegí el escenario de aduana: " + string.Join(" o ", EscenariosAduana.Todos));
        }
        if (dto.ContenedorTara < 0) return (null, "La tara del contenedor no puede ser negativa");
        var (cuit, errorCuit) = Cuit.Normalizar(dto.ExportadorCuit);
        if (errorCuit != null) return (null, "Exportador: " + errorCuit);
        var (ataCuit, errorAta) = Cuit.Normalizar(dto.AtaCuit);
        if (errorAta != null) return (null, "ATA: " + errorAta);

        return (new DatosAduana
        {
            Escenario = dto.Escenario,
            Exportador = Vacio(dto.Exportador),
            ExportadorCuit = cuit,
            OperacionId = Vacio(dto.OperacionId),
            Ata = Vacio(dto.Ata),
            AtaCuit = ataCuit,
            DestinacionAduanera = Vacio(dto.DestinacionAduanera),
            ProcedenciaDestino = Vacio(dto.ProcedenciaDestino),
            IdDestino = Vacio(dto.IdDestino),
            Lot = Vacio(configs.GetValueOrDefault(ConfigKeys.AduanaLot)),
            ChoferTipoDocumento = Vacio(dto.ChoferTipoDocumento) ?? Vacio(req.TipoDocumento),
            ChoferNacionalidad = Vacio(dto.ChoferNacionalidad) ?? Vacio(req.Nacionalidad),
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

    /// <summary>
    /// Toma el próximo número del contador. Si ese ticket ya existe (por ejemplo, con "solo número" la numeración
    /// arrancó por debajo de uno importado), se saltea hasta uno libre.
    /// </summary>
    private async Task<string> ProximoTicket(Empresa empresa, int puesto, bool soloNumero)
    {
        while (true)
        {
            var ticket = Tickets.Formatear(puesto, await ProximoNumero(empresa, puesto), soloNumero);
            if (!await _db.Pesadas.IgnoreQueryFilters().AnyAsync(p => p.EmpresaId == empresa.Id && p.TicketNumero == ticket)) return ticket;
        }
    }

    private async Task<int> ProximoNumero(Empresa empresa, int puesto)
    {
        var contador = _db.ContadoresTicket.Local.FirstOrDefault(c => c.EmpresaId == empresa.Id && c.Puesto == puesto)
            ?? await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == empresa.Id && c.Puesto == puesto);
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
    /// se descarta lo hecho y se vuelve a intentar con el número siguiente. Entre intentos se espera un poco al azar
    /// para que las que chocaron no vuelvan a chocar todas juntas.
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
                await Task.Delay(Random.Shared.Next(10, 40 * intento));
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
        datos.TipoDocumento = p.TipoDocumento ?? datos.TipoDocumento;
        datos.Nacionalidad = p.Nacionalidad ?? datos.Nacionalidad;
    }

    private async Task EncolarWorms(Dictionary<string, string> configs, Pesada pesada, params string[] eventos)
    {
        if (!WormsOutbox.EmpresaEnvia(configs, pesada.EmpresaId)) return;
        var empresa = await _db.Empresas.AsNoTracking().Where(e => e.Id == pesada.EmpresaId).Select(e => e.Nombre).FirstOrDefaultAsync() ?? "";
        foreach (var evento in eventos) WormsOutbox.Encolar(_db, configs, pesada, evento, empresa);
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
