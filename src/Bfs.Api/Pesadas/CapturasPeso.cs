using System.Collections.Concurrent;

namespace Bfs.Api.Pesadas;

/// <summary>Un peso tomado de la balanza en un momento, para guardar la pesada después de cargar los datos.</summary>
public record CapturaPeso(Guid Id, int Kilogramos, string? Balanza, DateTimeOffset En)
{
    /// <summary>Fotos de las cámaras sacadas en el momento de la captura (automatización). No van a la pantalla.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Automatizacion.FotoCamara> Fotos { get; init; } = [];

    /// <summary>Cuántas fotos se sacaron (para mostrarlo en pantalla).</summary>
    public int CantidadFotos => Fotos.Count;
}

/// <summary>
/// "Capturar peso, completar los datos y aceptar": con muchos camiones, el operador congela el peso apenas el camión
/// está quieto y carga los datos sin apuro. La captura vive en memoria del puesto (no sobrevive a un reinicio),
/// vence a los <see cref="Vigencia"/> y se usa una sola vez: se descarta recién cuando la pesada se guardó bien.
/// </summary>
public sealed class CapturasPeso(TimeProvider? reloj = null)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;
    private readonly ConcurrentDictionary<Guid, CapturaPeso> _capturas = new();

    public CapturaPeso Guardar(int kilogramos, string? balanza, IReadOnlyList<Automatizacion.FotoCamara>? fotos = null)
    {
        Limpiar();
        var c = new CapturaPeso(Guid.NewGuid(), kilogramos, balanza, _reloj.GetUtcNow()) { Fotos = fotos ?? [] };
        _capturas[c.Id] = c;
        return c;
    }

    /// <summary>La captura si existe y no venció. No la consume: ver <see cref="Usar"/>.</summary>
    public CapturaPeso? Buscar(Guid id) =>
        _capturas.TryGetValue(id, out var c) && _reloj.GetUtcNow() - c.En <= Vigencia ? c : null;

    /// <summary>Ya se guardó la pesada con este peso: no se puede volver a usar.</summary>
    public void Usar(Guid id) => _capturas.TryRemove(id, out _);

    private void Limpiar()
    {
        var limite = _reloj.GetUtcNow() - Vigencia;
        foreach (var (id, c) in _capturas)
        {
            if (c.En < limite) _capturas.TryRemove(id, out _);
        }
    }
}
