using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Automatizacion;

public record EstadoDispositivo(string Tipo, string Nombre, int Punto, int Balanza, string Lado, bool Activo);

public record EstadoAutomatizacion(
    bool Habilitada,
    bool Simulada,
    bool Conectada,
    string? Error,
    bool[] Entradas,
    bool[] Salidas,
    IReadOnlyList<EstadoDispositivo> Dispositivos,
    IReadOnlyList<string> Camaras,
    bool SecuenciaAutomatica)
{
    public static EstadoAutomatizacion Apagada { get; } =
        new(false, false, false, null, new bool[ConfigAutomatizacion.PuntosPlaca], new bool[ConfigAutomatizacion.PuntosPlaca], [], [], false);
}

/// <summary>
/// Placa de entradas / salidas de la portería (semáforos, barreras, sensores infrarrojos) — la 3.0 "Automatización".
/// Apagada (lo normal) no abre ninguna conexión: cada 5 segundos mira si BFS la habilitó. Prendida, lee la placa
/// cada 200 ms, reconecta sola si se corta y lleva la secuencia automática si está activada.
/// </summary>
public sealed class AutomatizacionService(IServiceScopeFactory scopes, ILogger<AutomatizacionService> log) : BackgroundService
{
    public static readonly TimeSpan Lectura = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan Recarga = TimeSpan.FromSeconds(5);

    /// <summary>Sin sensores no se sabe cuándo salió el camión: la secuencia vuelve a cero sola después de esto.</summary>
    public static readonly TimeSpan SalidaSinSensores = TimeSpan.FromSeconds(45);

    private readonly object _bloqueo = new();
    private ConfigAutomatizacion _config = new();
    private IPlacaES? _placa;
    private string _placaClave = "";
    private bool[] _entradas = new bool[ConfigAutomatizacion.PuntosPlaca];
    private bool[] _salidas = new bool[ConfigAutomatizacion.PuntosPlaca];
    private string? _error;
    private Dictionary<Guid, int> _ordenPorId = [];
    private Dictionary<string, int> _ordenPorNombre = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _proximaRecarga = DateTimeOffset.MinValue;

    // Secuencia automática, por balanza (1 y 2): esperando que el camión se vaya, si estaba arriba y desde cuándo.
    private readonly bool[] _esperaSalida = new bool[3];
    private readonly bool[] _ocupada = new bool[3];
    private readonly DateTimeOffset[] _esperaDesde = new DateTimeOffset[3];

    public ConfigAutomatizacion Config => _config;

    /// <summary>La placa simulada, si es la que está en uso (para cambiar sus entradas desde la pantalla de prueba).</summary>
    public PlacaSimulada? Simulada => _placa as PlacaSimulada;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >= _proximaRecarga) await RecargarAsync(stoppingToken);
                if (_config.Habilitada)
                {
                    await LeerAsync(stoppingToken);
                    await SecuenciaAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _error = ex.Message;
                log.LogDebug(ex, "Automatización");
            }

            try { await Task.Delay(_config.Habilitada ? Lectura : Recarga, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _placa?.Dispose();
    }

    /// <summary>Lee la configuración y las balanzas (para saber cuál es la 1 y cuál la 2). Se llama sola cada 5 s y al guardar.</summary>
    public async Task RecargarAsync(CancellationToken ct = default)
    {
        _proximaRecarga = DateTimeOffset.UtcNow + Recarga;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
        var json = await db.Configuraciones.AsNoTracking()
            .Where(c => c.Clave == ConfigKeys.AutomatizacionJson && c.EmpresaId == null).Select(c => c.Valor).FirstOrDefaultAsync(ct);
        var config = ConfigAutomatizacion.Parse(json);
        var balanzas = await db.Balanzas.AsNoTracking().Where(b => b.Activa).OrderBy(b => b.Orden).ThenBy(b => b.Nombre)
            .Select(b => new { b.Id, b.Nombre }).ToListAsync(ct);

        lock (_bloqueo)
        {
            _config = config;
            _ordenPorId = balanzas.Select((b, i) => (b.Id, i + 1)).ToDictionary(x => x.Id, x => x.Item2);
            _ordenPorNombre = balanzas.Select((b, i) => (b.Nombre, i + 1)).ToDictionary(x => x.Nombre, x => x.Item2, StringComparer.OrdinalIgnoreCase);

            var clave = config.Habilitada ? (config.Simulada ? "sim" : $"{config.ModbusIp}:{config.ModbusPuerto}:{config.ModbusUnidad}") : "";
            if (clave != _placaClave)
            {
                _placa?.Dispose();
                _placa = clave == "" ? null : config.Simulada ? new PlacaSimulada() : new PlacaModbus(config.ModbusIp, config.ModbusPuerto, (byte)config.ModbusUnidad);
                _placaClave = clave;
                _entradas = new bool[ConfigAutomatizacion.PuntosPlaca];
                _salidas = new bool[ConfigAutomatizacion.PuntosPlaca];
                _error = null;
            }
        }
    }

    private async Task LeerAsync(CancellationToken ct)
    {
        var placa = _placa;
        if (placa == null) return;
        try
        {
            if (!placa.Conectada) await placa.ConectarAsync(ct);
            var entradas = await placa.LeerEntradasAsync(ct);
            var salidas = await placa.LeerSalidasAsync(ct);
            lock (_bloqueo)
            {
                _entradas = entradas;
                _salidas = salidas;
                _error = null;
            }
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            lock (_bloqueo)
            {
                _entradas = new bool[ConfigAutomatizacion.PuntosPlaca];
                _error = $"La placa {_config.ModbusIp}:{_config.ModbusPuerto} no responde ({ex.Message})";
            }
            // No martillar una placa apagada: se reintenta en un rato.
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    public EstadoAutomatizacion Estado()
    {
        lock (_bloqueo)
        {
            var c = _config;
            if (!c.Habilitada) return EstadoAutomatizacion.Apagada;
            var dispositivos = c.Dispositivos.Where(d => d.Instalado).Select(d => new EstadoDispositivo(d.Tipo, d.Nombre, d.Punto, d.Balanza, d.Lado,
                TiposDispositivo.EsSalida(d.Tipo) ? _salidas[d.Punto - 1] : _entradas[d.Punto - 1])).ToList();
            return new EstadoAutomatizacion(true, c.Simulada, _placa?.Conectada == true && _error == null, _error,
                (bool[])_entradas.Clone(), (bool[])_salidas.Clone(), dispositivos,
                c.Camaras.Where(x => x.Habilitada).Select(x => x.Nombre).ToList(), c.SecuenciaAutomatica);
        }
    }

    /// <summary>1 o 2 según el orden de las balanzas (sin id: la primera).</summary>
    public int OrdenBalanza(Guid? balanzaId) =>
        balanzaId is { } id && _ordenPorId.TryGetValue(id, out var o) ? o : 1;

    /// <summary>
    /// Como la 3.x: con sensores instalados en esa balanza, el peso solo se toma con todos activos (camión entero arriba).
    /// Devuelve el motivo por el que no se puede, o null.
    /// </summary>
    public string? BloqueoCaptura(Guid? balanzaId)
    {
        lock (_bloqueo)
        {
            if (!_config.Habilitada) return null;
            var orden = OrdenBalanza(balanzaId);
            var sensores = _config.Instalados(TiposDispositivo.Sensor, orden).ToList();
            if (sensores.Count == 0) return null;
            if (_placa?.Conectada != true || _error != null) return "La placa de automatización no responde: no se pueden leer los sensores de la balanza";
            var libres = sensores.Where(s => !_entradas[s.Punto - 1]).Select(s => s.Nombre).ToList();
            return libres.Count == 0 ? null : $"Posicionar el camión dentro de la balanza ({string.Join(" y ", libres)} sin cortar)";
        }
    }

    /// <summary>
    /// Prende o apaga una salida. Si es un semáforo que se pone en verde, los otros semáforos de esa balanza pasan a rojo
    /// (como la 3.x: nunca dos verdes en la misma balanza).
    /// </summary>
    public async Task<string?> CambiarSalidaAsync(int punto, bool valor, CancellationToken ct = default)
    {
        var placa = _placa;
        if (!_config.Habilitada || placa == null) return "La automatización no está habilitada";
        if (punto is < 1 or > ConfigAutomatizacion.PuntosPlaca) return "Salida inválida";
        try
        {
            if (!placa.Conectada) await placa.ConectarAsync(ct);
            var dispositivo = _config.Dispositivos.FirstOrDefault(d => d.Instalado && TiposDispositivo.EsSalida(d.Tipo) && d.Punto == punto);
            if (valor && dispositivo?.Tipo == TiposDispositivo.Semaforo)
            {
                foreach (var otro in _config.Instalados(TiposDispositivo.Semaforo, dispositivo.Balanza).Where(s => s.Punto != punto))
                {
                    await placa.EscribirSalidaAsync(otro.Punto, false, ct);
                }
            }
            await placa.EscribirSalidaAsync(punto, valor, ct);
            var salidas = await placa.LeerSalidasAsync(ct);
            lock (_bloqueo) _salidas = salidas;
            return null;
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            return $"No se pudo cambiar la salida {punto}: {ex.Message}";
        }
    }

    /// <summary>Secuencia automática: la pesada de esa balanza quedó aceptada, el camión puede salir.</summary>
    public async Task PesadaAceptadaAsync(Guid? balanzaId, CancellationToken ct = default)
    {
        var c = _config;
        if (!c.Habilitada || !c.SecuenciaAutomatica) return;
        var orden = OrdenBalanza(balanzaId);
        foreach (var s in c.Instalados(TiposDispositivo.Semaforo, orden).Where(d => d.Lado == LadosBalanza.Salida))
        {
            await CambiarSalidaAsync(s.Punto, true, ct);
        }
        foreach (var b in c.Instalados(TiposDispositivo.Barrera, orden).Where(d => d.Lado == LadosBalanza.Salida))
        {
            await CambiarSalidaAsync(b.Punto, true, ct);
        }
        lock (_bloqueo)
        {
            _esperaSalida[orden] = true;
            _esperaDesde[orden] = DateTimeOffset.UtcNow;
            _ocupada[orden] = Ocupada(orden);
        }
    }

    /// <summary>Cuando el camión que estaba esperando se fue (sensores libres), la balanza vuelve a esperar al próximo.</summary>
    private async Task SecuenciaAsync(CancellationToken ct)
    {
        if (!_config.SecuenciaAutomatica) return;
        for (var orden = 1; orden <= 2; orden++)
        {
            bool reiniciar;
            lock (_bloqueo)
            {
                if (!_esperaSalida[orden]) continue;
                var tieneSensores = _config.Instalados(TiposDispositivo.Sensor, orden).Any();
                var ocupada = Ocupada(orden);
                reiniciar = tieneSensores
                    ? _ocupada[orden] && !ocupada
                    : DateTimeOffset.UtcNow - _esperaDesde[orden] > SalidaSinSensores;
                _ocupada[orden] = ocupada;
                if (reiniciar) _esperaSalida[orden] = false;
            }
            if (!reiniciar) continue;

            foreach (var b in _config.Instalados(TiposDispositivo.Barrera, orden)) await CambiarSalidaAsync(b.Punto, false, ct);
            foreach (var s in _config.Instalados(TiposDispositivo.Semaforo, orden).Where(d => d.Lado == LadosBalanza.Salida))
            {
                await CambiarSalidaAsync(s.Punto, false, ct);
            }
            foreach (var s in _config.Instalados(TiposDispositivo.Semaforo, orden).Where(d => d.Lado == LadosBalanza.Entrada))
            {
                await CambiarSalidaAsync(s.Punto, true, ct);
            }
        }
    }

    private bool Ocupada(int orden) => _config.Instalados(TiposDispositivo.Sensor, orden).Any(s => _entradas[s.Punto - 1]);

    /// <summary>Solo con la placa simulada: prender o apagar una entrada (como si cortaran un sensor).</summary>
    public bool SimularEntrada(int punto, bool valor)
    {
        if (_placa is not PlacaSimulada sim || punto is < 1 or > ConfigAutomatizacion.PuntosPlaca) return false;
        sim.Entradas[punto - 1] = valor;
        lock (_bloqueo) _entradas[punto - 1] = valor;
        return true;
    }

    /// <summary>Para los tests: una lectura ya, sin esperar el ciclo.</summary>
    public Task LeerAhoraAsync(CancellationToken ct = default) => LeerAsync(ct);

    public Task SecuenciaAhoraAsync(CancellationToken ct = default) => SecuenciaAsync(ct);
}
