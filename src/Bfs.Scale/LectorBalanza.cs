using Bfs.Core;
using Bfs.Scale.Tramas;
using Microsoft.Extensions.Logging;

namespace Bfs.Scale;

/// <summary>
/// Lee una balanza en segundo plano. Si el puerto no abre o se corta, reintenta solo cada <see cref="Reintento"/>.
/// No escribe cada trama a disco: guarda las últimas en memoria para el diagnóstico y loguea solo los cambios de estado.
/// </summary>
internal sealed class LectorBalanza : IDisposable
{
    public const int TramasGuardadas = 20;

    private readonly BalanzaConfig _config;
    private readonly IPuertoSerieFactory _puertos;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly TimeSpan _sinSenal;
    private readonly CancellationTokenSource _cts = new();
    private readonly DetectorEstabilidad _estabilidad = new();
    private readonly Queue<string> _tramas = new();
    private readonly object _lock = new();
    private LecturaBalanza _actual;
    private DateTimeOffset _ultimaTrama;
    private string? _ultimoErrorLogueado;
    private Stream? _stream;

    public LectorBalanza(BalanzaConfig config, IPuertoSerieFactory puertos, TimeProvider clock, ILogger log,
        TimeSpan? reintento = null, TimeSpan? sinSenal = null)
    {
        _config = config;
        _puertos = puertos;
        _clock = clock;
        _log = log;
        Reintento = reintento ?? TimeSpan.FromSeconds(5);
        _sinSenal = sinSenal ?? TimeSpan.FromSeconds(5);
        _actual = new LecturaBalanza(config.Id, config.Nombre, 0, false, "cerrada", config.Puerto, clock.GetUtcNow());
    }

    public TimeSpan Reintento { get; }
    public BalanzaConfig Config => _config;

    /// <summary>Si el puerto está abierto pero hace rato que no llega nada, se informa "sin señal" (cable, indicador apagado).</summary>
    public LecturaBalanza Actual
    {
        get
        {
            var a = _actual;
            return a.Estado == "abierta" && _clock.GetUtcNow() - _ultimaTrama > _sinSenal
                ? a with { Estado = "sin señal", Estable = false }
                : a;
        }
    }

    public IReadOnlyList<string> Tramas
    {
        get { lock (_lock) return _tramas.ToArray(); }
    }

    public void Iniciar()
    {
        var ct = _cts.Token;
        _ = _config.Modo == ModosBalanza.Simulada
            ? Task.Run(() => Simular(ct), ct)
            : Task.Run(() => Leer(ct), ct);
    }

    private async Task Leer(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using (var stream = _puertos.Abrir(_config))
                {
                    _stream = stream;
                    _ultimaTrama = _clock.GetUtcNow();
                    _estabilidad.Reiniciar();
                    _actual = _actual with { Estado = "abierta", Error = null, En = _ultimaTrama };
                    _ultimoErrorLogueado = null;
                    _log.LogInformation("Balanza {Balanza}: puerto {Puerto} abierto", _config.Nombre, _config.Puerto);
                    await LeerTramas(stream, ct);
                }
                if (!ct.IsCancellationRequested) Fallo("El puerto se cerró");
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Fallo(Traducir(ex));
            }
            finally
            {
                _stream = null;
            }

            try { await Task.Delay(Reintento, _clock, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task LeerTramas(Stream stream, CancellationToken ct)
    {
        var ensamblador = new EnsambladorTramas(_config.Protocolo);
        var buffer = new byte[256];
        while (!ct.IsCancellationRequested)
        {
            var n = await stream.ReadAsync(buffer, ct);
            if (n == 0) return;
            foreach (var trama in ensamblador.Agregar(buffer.AsSpan(0, n)))
            {
                Procesar(trama);
            }
        }
    }

    private void Procesar(string trama)
    {
        lock (_lock)
        {
            _tramas.Enqueue(Visible(trama));
            while (_tramas.Count > TramasGuardadas) _tramas.Dequeue();
        }

        var kg = ExtractorPeso.Extraer(trama, _config.Extraccion);
        if (kg == null) return;
        var ahora = _clock.GetUtcNow();
        _ultimaTrama = ahora;
        _actual = _actual with { Kilogramos = kg.Value, Estable = _estabilidad.Registrar(kg.Value, ahora), Estado = "abierta", En = ahora, Error = null };
    }

    /// <summary>Los errores de Windows al abrir un COM vienen en inglés; los dos que ve siempre el instalador, en castellano.</summary>
    private string Traducir(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => $"El puerto {_config.Puerto} no existe en esta PC",
        UnauthorizedAccessException when ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
            => $"El puerto {_config.Puerto} está en uso por otro programa",
        _ => ex.Message
    };

    private void Fallo(string error)
    {
        _actual = _actual with { Estado = "error", Estable = false, Error = error, En = _clock.GetUtcNow() };
        if (error != _ultimoErrorLogueado)
        {
            _log.LogWarning("Balanza {Balanza} ({Puerto}): {Error}. Se reintenta cada {Segundos} s",
                _config.Nombre, _config.Puerto, error, Reintento.TotalSeconds);
            _ultimoErrorLogueado = error;
        }
    }

    private async Task Simular(CancellationToken ct)
    {
        var random = new Random();
        var baseKg = 18240;
        _actual = _actual with { Estado = "simulada", Puerto = "SIM" };
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // De vez en cuando "sube un camión"; el resto del tiempo el peso queda quieto para poder probar la estabilidad.
                if (random.NextDouble() < 0.05) baseKg = random.Next(8000, 45000);
                var ahora = _clock.GetUtcNow();
                _actual = _actual with { Kilogramos = baseKg, Estable = _estabilidad.Registrar(baseKg, ahora), En = ahora };
                await Task.Delay(TimeSpan.FromMilliseconds(400), _clock, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>La trama con los caracteres de control a la vista: "\x02  12480kg\x03".</summary>
    public static string Visible(string trama) =>
        string.Concat(trama.Select(c => c is >= ' ' and <= '~' ? c.ToString() : $"\\x{(int)c:X2}"));

    public void Dispose()
    {
        _cts.Cancel();
        try { _stream?.Dispose(); } catch (IOException) { }
        _cts.Dispose();
    }
}
