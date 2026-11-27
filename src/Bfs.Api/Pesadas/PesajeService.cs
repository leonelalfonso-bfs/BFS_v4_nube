using System.Text.Json;
using Bfs.Api.Automatizacion;
using Bfs.Api.Config;
using Bfs.Api.Licencias;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations.Centum;
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

    private readonly Edicion _edicion;
    private readonly BfsOptions _opciones;
    private readonly CapturasPeso _capturas;
    private readonly AutomatizacionPesaje? _auto;
    private readonly Bfs.Integrations.DespachadorOutbox? _despachador;

    public PesajeService(BfsDbContext db, IBalanzas balanzas, ConfigService config, LicenciaService licencia, PasswordHasher<Usuario> hasher,
        BfsOptions? opciones = null, CapturasPeso? capturas = null, AutomatizacionPesaje? automatizacion = null,
        Bfs.Integrations.DespachadorOutbox? despachador = null)
    {
        _opciones = opciones ?? new BfsOptions();
        _auto = automatizacion;
        _despachador = despachador;
        _capturas = capturas ?? new CapturasPeso();
        _edicion = opciones?.Edicion ?? Ediciones.Compilada;
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

        var (peso, detalle) = TomarPesoCompuesto(req.ModoCaptura, req.Partes, req.PesoEntrada, req.ClaveManual, req.BalanzaId, op, configs, req.CapturaId);
        if (peso.Error != null) return peso.Error;
        pesada!.PesoEntrada = peso.Kg;
        pesada.Balanza = peso.Balanza;
        pesada.EntradaManualPor = peso.Manual ? op.Email : null;
        if (detalle != null) pesada.PartesJson = new PartesPesada(Entrada: detalle).Escribir();
        var (capturaFotos, manualFotos) = CapturaDeFotos(req.Partes, req.CapturaId, req.PesoEntrada);
        await AgregarFotos(pesada, LadosBalanza.Entrada, capturaFotos, manualFotos);

        // Control de carga por ejes en una sola pasada: el ticket se cierra acá, con el total y sin neto de mercadería.
        var unaPasada = detalle?.Modo == ModosCaptura.Ejes && configs.GetValueOrDefault(ConfigKeys.EjesPasadas) == "una";
        if (unaPasada)
        {
            pesada.Modo = ModosPesaje.ControlEjes;
            pesada.PesoSalida = 0;
            pesada.OperadorSalida = op.Email;
            pesada.TaraPallets = 0;
            Cerrar(pesada);
        }

        _db.Pesadas.Add(pesada);
        await RecordarVehiculo(pesada);
        if (unaPasada) await EncolarWorms(configs, pesada, WormsEventos.Entrada, WormsEventos.Salida);
        else await EncolarWorms(configs, pesada, WormsEventos.Entrada);
        await _db.SaveChangesAsync();
        UsarCapturas(req.Partes, req.CapturaId, req.PesoEntrada);
        await Aceptada(req.BalanzaId);
        return ResultadoPesaje.Ok(pesada);
    });

    /// <summary>
    /// Congela el peso de la balanza para guardar la pesada después (ver <see cref="CapturasPeso"/>).
    /// Con automatización, en el mismo momento saca las fotos (el camión todavía está arriba).
    /// </summary>
    /// <param name="parcial">Una parte o un eje: en una balanza corta o de ejes el camión nunca está entero arriba, así que
    /// no se pide que los sensores de las puntas estén libres.</param>
    public async Task<(CapturaPeso? Captura, ResultadoPesaje? Error)> CapturarAsync(Guid? balanzaId, Operador op, bool parcial = false)
    {
        var configs = await _config.GlobalAsync();
        var peso = TomarPeso(null, null, balanzaId, op, configs, parcial: parcial);
        if (peso.Error != null) return (null, peso.Error);
        var fotos = _auto != null ? await _auto.FotosAsync() : [];
        return (_capturas.Guardar(peso.Kg, peso.Balanza, fotos, peso.BalanzaId), null);
    }

    /// <summary>
    /// Próximo número de ticket que le tocaría a cada empresa activa en este puesto, sin consumir el contador.
    /// </summary>
    public async Task<Dictionary<Guid, string>> ProximosTicketsAsync()
    {
        var configs = await _config.GlobalAsync();
        var puesto = int.TryParse(configs.GetValueOrDefault(ConfigKeys.Puesto), out var p) && p is >= 1 and <= 99 ? p : 1;
        var soloNumero = Si(configs, ConfigKeys.TicketSoloNumero);
        var compartida = Ediciones.NumeracionCompartida(_edicion);

        var empresas = await _db.Empresas.AsNoTracking().Where(e => e.Activa).ToListAsync();
        var contadores = await _db.ContadoresTicket.AsNoTracking().Where(c => c.Puesto == puesto).ToListAsync();
        var usados = await _db.Pesadas.IgnoreQueryFilters().AsNoTracking()
            .Select(x => new { x.EmpresaId, x.TicketNumero })
            .ToListAsync();

        string LibreDesde(int inicial, HashSet<string> ocupados)
        {
            for (var n = Math.Max(1, inicial); ; n++)
            {
                var t = Tickets.Formatear(puesto, n, soloNumero);
                if (!ocupados.Contains(t)) return t;
            }
        }

        var resultado = new Dictionary<Guid, string>();
        if (compartida)
        {
            var traia = await _db.Empresas.IgnoreQueryFilters().Select(e => (int?)e.ProximoTicket).MaxAsync() ?? 1;
            var inicial = contadores.FirstOrDefault(c => c.EmpresaId == ContadoresCompartidos.Empresa)?.Proximo
                ?? (puesto == 1 ? Math.Max(1, traia) : 1);
            var ocupados = usados.Select(u => u.TicketNumero).ToHashSet(StringComparer.Ordinal);
            var ticket = LibreDesde(inicial, ocupados);
            foreach (var e in empresas) resultado[e.Id] = ticket;
            return resultado;
        }

        var porEmpresa = usados.GroupBy(u => u.EmpresaId)
            .ToDictionary(g => g.Key, g => g.Select(u => u.TicketNumero).ToHashSet(StringComparer.Ordinal));
        foreach (var e in empresas)
        {
            var inicial = contadores.FirstOrDefault(c => c.EmpresaId == e.Id)?.Proximo
                ?? (puesto == 1 ? Math.Max(1, e.ProximoTicket) : 1);
            var ocupados = porEmpresa.GetValueOrDefault(e.Id) ?? [];
            resultado[e.Id] = LibreDesde(inicial, ocupados);
        }
        return resultado;
    }

    /// <summary>
    /// Fotos de la entrada o la salida: las de la captura si se capturó; si no (peso manual o leído al aceptar), se sacan ahora.
    /// </summary>
    private async Task AgregarFotos(Pesada pesada, string momento, Guid? capturaId, int? manual)
    {
        if (_auto == null) return;
        var fotos = capturaId is { } id && manual == null && _capturas.Buscar(id) is { } c ? c.Fotos : await _auto.FotosAsync();
        _auto.Agregar(pesada, momento, fotos);
    }

    /// <summary>Secuencia automática (si está activada): semáforo y barrera de salida de esa balanza.</summary>
    private Task Aceptada(Guid? balanzaId) => _auto?.AceptadaAsync(balanzaId) ?? Task.CompletedTask;

    public Task<ResultadoPesaje> RegistrarSalidaAsync(Guid id, SalidaRequest req, Operador op) => ConReintento(async () =>
    {
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == EstadosPesada.Salida) return ResultadoPesaje.Invalida("Ya tiene salida");
        if (pesada.Estado == EstadosPesada.Anulada) return ResultadoPesaje.Invalida("La pesada está anulada");

        var configs = await _config.GlobalAsync();
        if (_edicion == Edicion.Inagro && req.Datos is { } datosSalida)
        {
            var errorDatos = await AplicarDatosSalidaInagro(pesada, datosSalida, configs);
            if (errorDatos != null) return errorDatos;
        }
        var (peso, detalle) = TomarPesoCompuesto(req.ModoCaptura, req.Partes, req.PesoSalida, req.ClaveManual, req.BalanzaId, op, configs, req.CapturaId);
        if (peso.Error != null) return peso.Error;
        if (detalle != null) pesada.PartesJson = (PartesPesada.Leer(pesada.PartesJson) with { Salida = detalle }).Escribir();

        pesada.PesoSalida = peso.Kg;
        pesada.BalanzaSalida = peso.Balanza;
        pesada.SalidaManualPor = peso.Manual ? op.Email : null;
        pesada.OperadorSalida = op.Email;
        pesada.TaraPallets = TaraPalletsHabilitada(configs) ? Math.Max(0, req.TaraPallets ?? pesada.TaraPallets) : 0;
        Cerrar(pesada);
        var (capturaFotos, manualFotos) = CapturaDeFotos(req.Partes, req.CapturaId, req.PesoSalida);
        await AgregarFotos(pesada, LadosBalanza.Salida, capturaFotos, manualFotos);
        await EncolarWorms(configs, pesada, WormsEventos.Salida);
        await EncolarCentum(configs, pesada);
        await _db.SaveChangesAsync();
        UsarCapturas(req.Partes, req.CapturaId, req.PesoSalida);
        await Aceptada(req.BalanzaId);
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

        var bruto = TomarPeso(req.Datos.PesoEntrada, req.ClaveManual, req.Datos.BalanzaId, op, configs, req.Datos.CapturaId);
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
        await AgregarFotos(pesada, LadosBalanza.Entrada, req.Datos.CapturaId, req.Datos.PesoEntrada);

        _db.Pesadas.Add(pesada);
        await RecordarVehiculo(pesada);
        await EncolarWorms(configs, pesada, WormsEventos.Entrada, WormsEventos.Salida);
        await EncolarCentum(configs, pesada);
        await _db.SaveChangesAsync();
        UsarCaptura(req.Datos.CapturaId, req.Datos.PesoEntrada);
        await Aceptada(req.Datos.BalanzaId);
        return ResultadoPesaje.Ok(pesada);
    });

    /// <summary>La pesada quedó guardada con el peso capturado: esa captura ya no sirve para otra.</summary>
    private void UsarCaptura(Guid? capturaId, int? manual)
    {
        if (capturaId is { } id && manual == null) _capturas.Usar(id);
    }

    /// <summary>Pesa el vehículo vacío y guarda (o actualiza) su tara para usarla después en "Tara conocida".</summary>
    public async Task<(TaraConocida? Tara, ResultadoPesaje? Error)> TomarTaraAsync(TomarTaraRequest req, Operador op)
    {
        var configs = await _config.GlobalAsync();
        if (!Si(configs, ConfigKeys.TaraConocida)) return (null, ResultadoPesaje.Prohibida("La tara conocida no está habilitada"));
        var id = Patente.Normalizar(req.Identificador);
        if (id.Length == 0) return (null, ResultadoPesaje.Invalida("Falta la patente o el número de carro"));

        var peso = TomarPeso(req.PesoManual, req.ClaveManual, req.BalanzaId, op, configs, req.CapturaId);
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
        UsarCaptura(req.CapturaId, req.PesoManual);
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
        ("aduana.bultoId", "Bulto", p => p.Aduana?.BultoId, (p, v) => p.Aduana!.BultoId = v),
        // Edición Inagro. El peso de origen es lo que dice el remito del origen, no un peso de la balanza: se puede corregir.
        ("inagro.movimiento", "Id de operación", p => p.Inagro?.Movimiento?.ToString(), (p, v) =>
        {
            p.Inagro!.Movimiento = int.Parse(v!);
            p.Tipo = MovimientosInagro.Tipo(p.Inagro.Movimiento);
            if (!MovimientosInagro.LlevaPesoOrigen(p.Inagro.Movimiento)) p.Inagro.PesoOrigen = null;
        }),
        ("inagro.pesoOrigen", "Peso de origen", p => p.Inagro?.PesoOrigen?.ToString(), (p, v) => p.Inagro!.PesoOrigen = v == null ? null : int.Parse(v)),
        ("inagro.tanque", "Tanque", p => p.Inagro?.Tanque, (p, v) => p.Inagro!.Tanque = v),
        ("inagro.analisis", "Análisis", p => p.Inagro?.Analisis, (p, v) => p.Inagro!.Analisis = v),
        ("inagro.precintos", "Precintos", p => p.Inagro?.Precintos, (p, v) => p.Inagro!.Precintos = v),
        ("inagro.productoCodigo", "Código de producto", p => p.Inagro?.ProductoCodigo, (p, v) => p.Inagro!.ProductoCodigo = v),
        ("inagro.cuilChofer", "CUIL chofer", p => p.Inagro?.CuilChofer, (p, v) => p.Inagro!.CuilChofer = v),
        ("inagro.observacionesRemito", "Observaciones del remito", p => p.Inagro?.ObservacionesRemito, (p, v) => p.Inagro!.ObservacionesRemito = v)
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
            if (clave.StartsWith("inagro.", StringComparison.Ordinal) && pesada.Inagro == null)
            {
                return ResultadoPesaje.Invalida("La pesada no tiene datos de INAGRO");
            }
            if (clave == "inagro.pesoOrigen" && !MovimientosInagro.LlevaPesoOrigen(
                    req.Datos!.TryGetValue("inagro.movimiento", out var mov) && int.TryParse(mov, out var m) ? m : pesada.Inagro!.Movimiento)
                && !string.IsNullOrWhiteSpace(valor))
            {
                return ResultadoPesaje.Invalida("El peso de origen es solo para compras y órdenes de traspaso");
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
        if (_edicion == Edicion.Inagro && pesada.Inagro != null)
        {
            await RefrescarCatalogosInagro(pesada,
                actualizarCliente: req.Datos.ContainsKey("cliente"),
                actualizarTransporte: req.Datos.ContainsKey("transporte"),
                actualizarProducto: req.Datos.ContainsKey("producto") && !req.Datos.ContainsKey("inagro.productoCodigo"));
        }

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
            case "inagro.movimiento":
                return v is "1" or "2" or "3" ? (v, null) : (null, "El Id de operación es 1 (compras), 2 (ventas) o 3 (OT)");
            case "inagro.pesoOrigen":
                if (v == null) return (null, null);
                return int.TryParse(v, out var kg) && kg >= 0 ? (kg.ToString(), null) : (null, "El peso de origen son kilos (número entero)");
            case "inagro.analisis":
                return ValidarAnalisis(v);
            case "inagro.cuilChofer":
                return ValidarCuil(v);
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

        DatosInagro? inagro = null;
        if (_edicion == Edicion.Inagro)
        {
            var (datos, errorInagro) = await ArmarInagro(req, clienteId, configs);
            if (errorInagro != null) return (null, ResultadoPesaje.Invalida(errorInagro));
            inagro = datos;
        }

        var puesto = int.TryParse(configs.GetValueOrDefault(ConfigKeys.Puesto), out var p) && p is >= 1 and <= 99 ? p : 1;
        var soloNumero = Si(configs, ConfigKeys.TicketSoloNumero);
        var ticket = await ProximoTicket(empresa, puesto, soloNumero);

        var tipo = inagro != null
            ? MovimientosInagro.Tipo(inagro.Movimiento)
            : string.IsNullOrWhiteSpace(req.Tipo) ? configs.GetValueOrDefault(ConfigKeys.WormsTipoDefault, "INGRESO") : req.Tipo;

        return (new Pesada
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresa.Id,
            TicketNumero = ticket,
            Tipo = tipo,
            Inagro = inagro,
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
            TaraPallets = TaraPalletsHabilitada(configs) ? Math.Max(0, req.TaraPallets) : 0,
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

    /// <summary>
    /// Campos de INAGRO: valida lo que cargó el operador y le suma lo que la 3.x guardaba con la pesada
    /// (datos del cliente y del transporte del catálogo, código del producto y planta de la instalación).
    /// </summary>
    private async Task<(DatosInagro? Datos, string? Error)> ArmarInagro(EntradaRequest req, Guid? clienteId, Dictionary<string, string> configs)
    {
        var dto = req.Inagro;
        if (dto?.Movimiento is not (MovimientosInagro.Compras or MovimientosInagro.Ventas or MovimientosInagro.Traspaso))
        {
            return (null, "Elegí el Id de operación: compras, ventas u OT");
        }
        int? pesoOrigen = null;
        if (MovimientosInagro.LlevaPesoOrigen(dto.Movimiento) && dto.PesoOrigen is { } origen)
        {
            if (origen < 0) return (null, "El peso de origen no puede ser negativo");
            pesoOrigen = origen;
        }
        var (analisis, errorAnalisis) = ValidarAnalisis(Vacio(dto.Analisis));
        if (errorAnalisis != null) return (null, errorAnalisis);
        var (cuil, errorCuil) = ValidarCuil(Vacio(dto.CuilChofer));
        if (errorCuil != null) return (null, errorCuil);

        var cliente = clienteId.HasValue
            ? await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clienteId)
            : null;
        var transporte = await PorNombre(_db.Transportes, req.Transporte, t => t.RazonSocial);
        var producto = await PorNombre(_db.Productos, req.Producto, x => x.Nombre);

        return (new DatosInagro
        {
            Movimiento = dto.Movimiento,
            PesoOrigen = pesoOrigen,
            Tanque = Vacio(dto.Tanque),
            Analisis = analisis,
            Precintos = Vacio(dto.Precintos),
            ProductoCodigo = Vacio(dto.ProductoCodigo) ?? producto?.Codigo,
            CuilChofer = cuil,
            ObservacionesRemito = Vacio(dto.ObservacionesRemito),
            Planta = Vacio(configs.GetValueOrDefault(ConfigKeys.InagroPlanta)) ?? Inagro.PlantaPorDefecto,
            ClienteCuit = cliente?.Cuit,
            ClienteDireccion = cliente?.Direccion,
            ClienteLocalidad = cliente?.Localidad,
            ClienteCondicionIva = cliente?.CondicionIva,
            TransporteCuit = transporte?.Cuit,
            TransporteDireccion = transporte?.Direccion,
            TransporteLocalidad = transporte?.Localidad,
            TransporteCondicionIva = transporte?.CondicionIva
        }, null);
    }

    private static (string? Valor, string? Error) ValidarAnalisis(string? v)
    {
        if (v == null) return (null, null);
        return v.Length <= 6 && v.All(char.IsDigit) ? (v, null) : (null, "El análisis son hasta 6 números");
    }

    private static (string? Valor, string? Error) ValidarCuil(string? v)
    {
        if (v == null) return (null, null);
        var digitos = Inagro.Digitos(v, int.MaxValue);
        return digitos.Length == 11 && v.All(c => char.IsDigit(c) || c is '-' or ' ' or '.')
            ? (digitos, null)
            : (null, "El CUIL del chofer son 11 números");
    }

    /// <summary>El del catálogo con ese nombre exacto (sin distinguir mayúsculas), si hay uno solo.</summary>
    private static async Task<T?> PorNombre<T>(IQueryable<T> set, string? nombre, System.Linq.Expressions.Expression<Func<T, string>> columna)
        where T : EntidadSync
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        var buscado = nombre.Trim().ToLower();
        var param = columna.Parameters[0];
        var cond = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Call(columna.Body, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
                System.Linq.Expressions.Expression.Constant(buscado)),
            param);
        var encontrados = await set.AsNoTracking().Where(cond).Take(2).ToListAsync();
        return encontrados.Count == 1 ? encontrados[0] : null;
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

    private readonly record struct Peso(int Kg, string? Balanza, bool Manual, ResultadoPesaje? Error, Guid? BalanzaId = null);

    /// <summary>
    /// El peso escrito a mano necesita el permiso, que el ingreso manual esté habilitado y la clave si hay una.
    /// El de la balanza, que esté leyendo y (si BFS lo exige) estable. Con <paramref name="capturaId"/>, el que se capturó
    /// antes con <see cref="CapturarAsync"/> (ya se validó al capturarlo).
    /// </summary>
    private Peso TomarPeso(int? manual, string? clave, Guid? balanzaId, Operador op, Dictionary<string, string> configs, Guid? capturaId = null,
        bool parcial = false)
    {
        if (manual is { } kg)
        {
            if (ValidarManual(clave, op, configs) is { } error) return new Peso(0, null, true, error);
            return kg > 0 ? new Peso(kg, null, true, null) : new Peso(0, null, true, ResultadoPesaje.Invalida("El peso tiene que ser mayor a cero"));
        }

        if (capturaId is { } id)
        {
            return _capturas.Buscar(id) is { } c
                ? new Peso(c.Kilogramos, c.Balanza, false, null, c.BalanzaId)
                : new Peso(0, null, false, ResultadoPesaje.Invalida("El peso capturado venció o ya se usó: capturalo de nuevo"));
        }

        // Automatización: con sensores instalados, solo con el camión entero sobre la balanza (como la 3.x).
        if (!parcial && _auto?.Bloqueo(balanzaId) is { } bloqueo) return new Peso(0, null, false, ResultadoPesaje.Invalida(bloqueo));

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
            ? new Peso(l.Kilogramos, l.Nombre, false, null, l.BalanzaId)
            : new Peso(0, l.Nombre, false, ResultadoPesaje.Invalida(errorLectura), l.BalanzaId);
    }

    /// <summary>
    /// El peso de una pasada: simple (una captura o un peso a mano) o compuesto (en partes o por ejes), sumando
    /// capturas que tomó el servidor. Por ejes, arma los grupos y los compara con los límites configurados.
    /// </summary>
    private (Peso Peso, PesoPorPartes? Detalle) TomarPesoCompuesto(string? modo, List<ParteRequest>? partes, int? manual, string? clave,
        Guid? balanzaId, Operador op, Dictionary<string, string> configs, Guid? capturaId)
    {
        static (Peso, PesoPorPartes?) Error(string mensaje, bool prohibida = false) =>
            (new Peso(0, null, false, prohibida ? ResultadoPesaje.Prohibida(mensaje) : ResultadoPesaje.Invalida(mensaje)), null);

        if (modo is null or ModosCaptura.Simple) return (TomarPeso(manual, clave, balanzaId, op, configs, capturaId), null);
        if (modo is not (ModosCaptura.Partes or ModosCaptura.Ejes)) return Error("Modo de pesaje inválido");
        if (modo == ModosCaptura.Partes && !Si(configs, ConfigKeys.PesajePartes)) return Error("El pesaje en partes no está habilitado", true);
        if (modo == ModosCaptura.Ejes && !Si(configs, ConfigKeys.PesajeEjes)) return Error("El pesaje por ejes no está habilitado", true);
        partes ??= [];
        if (modo == ModosCaptura.Partes && partes.Count < 2) return Error("Pesando en partes hacen falta al menos dos capturas (chasis y acoplado)");
        if (modo == ModosCaptura.Ejes && partes.Count == 0) return Error("Capturá el peso de cada eje");

        var registradas = new List<PartePeso>();
        string? balanza = null;
        var algunaManual = false;
        for (var i = 0; i < partes.Count; i++)
        {
            var p = partes[i];
            if (p.CapturaId == null && p.Kilogramos == null) return Error($"Falta capturar la parte {i + 1}");
            var peso = TomarPeso(p.Kilogramos, clave, balanzaId, op, configs, p.CapturaId, parcial: true);
            if (peso.Error != null) return (peso, null);
            balanza ??= peso.Balanza;
            algunaManual |= peso.Manual;
            var etiqueta = Vacio(p.Etiqueta) ?? (modo == ModosCaptura.Ejes ? $"Eje {i + 1}" : $"Parte {i + 1}");
            registradas.Add(new PartePeso(etiqueta, peso.Kg, peso.Manual));
        }

        List<GrupoEjes>? grupos = null;
        int? limiteTotal = null;
        if (modo == ModosCaptura.Ejes)
        {
            var limites = TiposEje.Limites(configs.GetValueOrDefault(ConfigKeys.EjesLimites));
            grupos = [];
            foreach (var g in partes.Select((p, i) => (p, i)).GroupBy(x => x.p.Grupo).OrderBy(g => g.Key))
            {
                var tipos = g.Select(x => x.p.TipoEje).Distinct().ToList();
                if (tipos.Count != 1 || TiposEje.Buscar(tipos[0]) is not { } tipo) return Error($"Tipo de eje inválido en el grupo {g.Key + 1}");
                if (g.Count() != tipo.Ejes) return Error($"El grupo {g.Key + 1} ({tipo.Nombre}) lleva {tipo.Ejes} ejes y se capturaron {g.Count()}");
                grupos.Add(new GrupoEjes(tipo.Id, tipo.Nombre, g.Select(x => registradas[x.i].Kilogramos).ToList(), limites[tipo.Id]));
            }
            limiteTotal = int.TryParse(configs.GetValueOrDefault(ConfigKeys.EjesLimiteTotal), out var t) && t > 0 ? t : TiposEje.LimiteTotalPorDefecto;
        }

        var detalle = new PesoPorPartes(modo, registradas, grupos, limiteTotal);
        return (new Peso(detalle.Total, balanza, algunaManual, null), detalle);
    }

    /// <summary>Las fotos de la pasada son las de la primera captura (el camión recién llegado a la balanza).</summary>
    private static (Guid? CapturaId, int? Manual) CapturaDeFotos(List<ParteRequest>? partes, Guid? capturaId, int? manual) =>
        partes is { Count: > 0 } ? (partes[0].CapturaId, partes[0].Kilogramos) : (capturaId, manual);

    private void UsarCapturas(List<ParteRequest>? partes, Guid? capturaId, int? manual)
    {
        if (partes is { Count: > 0 })
        {
            foreach (var p in partes) UsarCaptura(p.CapturaId, p.Kilogramos);
        }
        else
        {
            UsarCaptura(capturaId, manual);
        }
    }

    /// <summary>Configuración → General → Tara pallets. Apagada, no se descuenta nada aunque llegue un valor.</summary>
    private static bool TaraPalletsHabilitada(Dictionary<string, string> configs) => Si(configs, ConfigKeys.TaraPallets, "true");

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
        // Numeración compartida (INAGRO): un ticket no se repite en ninguna empresa.
        var compartida = Ediciones.NumeracionCompartida(_edicion);
        while (true)
        {
            var ticket = Tickets.Formatear(puesto, await ProximoNumero(empresa, puesto, compartida), soloNumero);
            var usado = await _db.Pesadas.IgnoreQueryFilters()
                .AnyAsync(p => (compartida || p.EmpresaId == empresa.Id) && p.TicketNumero == ticket);
            if (!usado) return ticket;
        }
    }

    private async Task<int> ProximoNumero(Empresa empresa, int puesto, bool compartida)
    {
        var clave = compartida ? ContadoresCompartidos.Empresa : empresa.Id;
        var contador = _db.ContadoresTicket.Local.FirstOrDefault(c => c.EmpresaId == clave && c.Puesto == puesto)
            ?? await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == clave && c.Puesto == puesto);
        if (contador == null)
        {
            // El puesto 1 sigue la numeración que traía la empresa (spike / importación de la 3.x);
            // la compartida, la más alta de todas.
            var traia = compartida
                ? await _db.Empresas.IgnoreQueryFilters().Select(e => (int?)e.ProximoTicket).MaxAsync() ?? 1
                : empresa.ProximoTicket;
            contador = new ContadorTicket { Id = Guid.NewGuid(), EmpresaId = clave, Puesto = puesto, Proximo = puesto == 1 ? Math.Max(1, traia) : 1 };
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
        if (_edicion == Edicion.Estandar || !WormsOutbox.EmpresaEnvia(configs, pesada.EmpresaId)) return;
        var empresa = await _db.Empresas.AsNoTracking().Where(e => e.Id == pesada.EmpresaId).Select(e => e.Nombre).FirstOrDefaultAsync() ?? "";
        foreach (var evento in eventos) WormsOutbox.Encolar(_db, configs, pesada, evento, empresa);
    }

    /// <summary>Edición Inagro: la pesada cerrada va a Centum si su empresa tiene una sociedad que vincula.</summary>
    private async Task EncolarCentum(Dictionary<string, string> configs, Pesada pesada)
    {
        if (_edicion != Edicion.Inagro || pesada.Inagro == null) return;
        var sociedad = await _db.Empresas.AsNoTracking().Where(e => e.Id == pesada.EmpresaId).Select(e => e.CodigoSociedad).FirstOrDefaultAsync();
        CentumOutbox.Encolar(_db, configs, pesada, sociedad, _opciones.HoraLocal(pesada.EntradaEn));
    }

    /// <summary>
    /// En INAGRO muchos datos del viaje (cliente, tanque, análisis de laboratorio, precintos, remito y peso de origen)
    /// se completan o confirman recién al cerrar la salida, justo antes de disparar el envío a Centum.
    /// </summary>
    private async Task<ResultadoPesaje?> AplicarDatosSalidaInagro(Pesada pesada, EntradaRequest d, Dictionary<string, string> configs)
    {
        if (d.EmpresaId != Guid.Empty && d.EmpresaId != pesada.EmpresaId)
        {
            if (!await _db.Empresas.AnyAsync(e => e.Id == d.EmpresaId)) return ResultadoPesaje.Invalida("Empresa inexistente");
            pesada.EmpresaId = d.EmpresaId;
        }

        var clienteId = await ResolverCliente(d.ClienteId, d.Cliente);
        var (inagro, errorInagro) = await ArmarInagro(d, clienteId, configs);
        if (errorInagro != null) return ResultadoPesaje.Invalida(errorInagro);

        pesada.PatenteAcoplado = string.IsNullOrWhiteSpace(d.PatenteAcoplado) ? null : Patente.Normalizar(d.PatenteAcoplado);
        pesada.Conductor = Vacio(d.Conductor);
        pesada.Documento = Vacio(d.Documento);
        pesada.TipoDocumento = Vacio(d.TipoDocumento);
        pesada.Nacionalidad = Vacio(d.Nacionalidad);
        pesada.Cliente = Vacio(d.Cliente);
        pesada.ClienteId = clienteId;
        pesada.Transporte = Vacio(d.Transporte);
        pesada.Producto = Vacio(d.Producto);
        pesada.Procedencia = Vacio(d.Procedencia);
        pesada.Destino = Vacio(d.Destino);
        pesada.Comprobante = Vacio(d.Comprobante);
        pesada.Observaciones = Vacio(d.Observaciones);
        pesada.Inagro = inagro;
        pesada.Tipo = MovimientosInagro.Tipo(inagro!.Movimiento);
        await RecordarVehiculo(pesada);
        return null;
    }

    private async Task RefrescarCatalogosInagro(Pesada pesada, bool actualizarCliente, bool actualizarTransporte, bool actualizarProducto)
    {
        var i = pesada.Inagro!;
        if (actualizarCliente)
        {
            var cliente = pesada.ClienteId.HasValue
                ? await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == pesada.ClienteId)
                : null;
            i.ClienteCuit = cliente?.Cuit;
            i.ClienteDireccion = cliente?.Direccion;
            i.ClienteLocalidad = cliente?.Localidad;
            i.ClienteCondicionIva = cliente?.CondicionIva;
        }
        if (actualizarTransporte)
        {
            var tr = await PorNombre(_db.Transportes, pesada.Transporte, t => t.RazonSocial);
            i.TransporteCuit = tr?.Cuit;
            i.TransporteDireccion = tr?.Direccion;
            i.TransporteLocalidad = tr?.Localidad;
            i.TransporteCondicionIva = tr?.CondicionIva;
        }
        if (actualizarProducto)
        {
            var prod = await PorNombre(_db.Productos, pesada.Producto, p => p.Nombre);
            if (prod?.Codigo != null) i.ProductoCodigo = prod.Codigo;
        }
    }

    /// <summary>
    /// Reenvía (o envía por primera vez) una pesada cerrada a Centum con sus datos actuales: útil si se pesó con Centum
    /// desactivado o si se corrigió el remito, análisis, producto o peso de origen después de cerrar.
    /// </summary>
    public async Task<ResultadoPesaje> EnviarCentumAsync(Guid id)
    {
        if (_edicion != Edicion.Inagro) return ResultadoPesaje.Inexistente();
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado != EstadosPesada.Salida) return ResultadoPesaje.Invalida("Solo se envían a Centum las pesadas cerradas");
        if (pesada.Inagro == null) return ResultadoPesaje.Invalida("La pesada no tiene datos de INAGRO");

        var sociedad = await _db.Empresas.AsNoTracking().Where(e => e.Id == pesada.EmpresaId).Select(e => e.CodigoSociedad).FirstOrDefaultAsync();
        if (!Inagro.SociedadEnvia(sociedad)) return ResultadoPesaje.Invalida("La empresa de esta pesada está configurada como 'No vincula Centum'");

        var configs = await _config.GlobalAsync();
        if (string.IsNullOrWhiteSpace(configs.GetValueOrDefault(ConfigKeys.CentumEndpoint)))
            return ResultadoPesaje.Invalida("Falta configurar la dirección del webhook en Configuración → API → Centum");

        var previos = await _db.Outbox
            .Where(o => o.Destino == CentumDestino.Destino && o.PesadaId == pesada.Id
                        && (o.Estado == Bfs.Integrations.EstadosOutbox.Pendiente || o.Estado == Bfs.Integrations.EstadosOutbox.Error))
            .ToListAsync();
        foreach (var m in previos) m.Estado = Bfs.Integrations.EstadosOutbox.Descartado;

        CentumOutbox.EncolarDirecto(_db, pesada, sociedad, _opciones.HoraLocal(pesada.EntradaEn));
        await _db.SaveChangesAsync();

        if (_despachador != null && Si(configs, ConfigKeys.CentumEnabled))
        {
            await _despachador.ProcesarAsync(_db);
        }
        return ResultadoPesaje.Ok(pesada);
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
