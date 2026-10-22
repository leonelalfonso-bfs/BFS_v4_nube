using Bfs.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bfs.Scale;

/// <summary>Todas las balanzas del nodo (la 3.x permitía varias; la 4.0 también).</summary>
public sealed class BalanzaManager : IBalanzas, IDisposable
{
    private readonly IPuertoSerieFactory _puertos;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;
    private readonly TimeSpan? _reintento;
    private readonly TimeSpan? _sinSenal;
    private readonly object _lock = new();
    private IReadOnlyList<LectorBalanza> _lectores = [];

    public BalanzaManager(IPuertoSerieFactory puertos, ILogger<BalanzaManager>? log = null, TimeProvider? clock = null,
        TimeSpan? reintento = null, TimeSpan? sinSenal = null)
    {
        _puertos = puertos;
        _log = (ILogger?)log ?? NullLogger.Instance;
        _clock = clock ?? TimeProvider.System;
        _reintento = reintento;
        _sinSenal = sinSenal;
    }

    public IReadOnlyList<LecturaBalanza> Lecturas => _lectores.Select(l => l.Actual).ToList();

    public LecturaBalanza Lectura(Guid? balanzaId = null)
    {
        var lectores = _lectores;
        var lector = balanzaId.HasValue ? lectores.FirstOrDefault(l => l.Config.Id == balanzaId) : lectores.FirstOrDefault();
        return lector?.Actual ?? LecturaBalanza.Ninguna;
    }

    public IReadOnlyList<string> UltimasTramas(Guid balanzaId) =>
        _lectores.FirstOrDefault(l => l.Config.Id == balanzaId)?.Tramas ?? [];

    public IReadOnlyList<string> Puertos()
    {
        try
        {
            return _puertos.Puertos();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return [];
        }
    }

    public void Aplicar(IReadOnlyList<BalanzaConfig> balanzas)
    {
        lock (_lock)
        {
            foreach (var l in _lectores) l.Dispose();
            var nuevos = balanzas.Select(b => new LectorBalanza(b, _puertos, _clock, _log, _reintento, _sinSenal)).ToList();
            _lectores = nuevos;
            foreach (var l in nuevos) l.Iniciar();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var l in _lectores) l.Dispose();
            _lectores = [];
        }
    }
}

/// <summary>En la nube no hay balanzas: el peso llega desde el nodo de portería.</summary>
public sealed class SinBalanzas : IBalanzas
{
    public IReadOnlyList<LecturaBalanza> Lecturas => [];
    public LecturaBalanza Lectura(Guid? balanzaId = null) => LecturaBalanza.Ninguna;
    public IReadOnlyList<string> UltimasTramas(Guid balanzaId) => [];
    public IReadOnlyList<string> Puertos() => [];
    public void Aplicar(IReadOnlyList<BalanzaConfig> balanzas) { }
}
