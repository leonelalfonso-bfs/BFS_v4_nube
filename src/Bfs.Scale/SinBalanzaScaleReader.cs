using Bfs.Core;

namespace Bfs.Scale;

/// <summary>En la nube no hay balanza conectada: el peso llega desde el nodo de portería.</summary>
public sealed class SinBalanzaScaleReader : IScaleReader, IScaleController
{
    public ScaleReading Current { get; } = new(0, "", "sin balanza", DateTimeOffset.UtcNow);
    public string Status => Current.Estado;
    public string? LastError => null;
    public ScaleSettings Settings { get; } = ScaleSettings.Simulated() with { Mode = "ninguna" };

    public event Action<ScaleReading>? WeightChanged { add { } remove { } }

    public IReadOnlyList<string> ListPorts() => [];
    public void Apply(ScaleSettings settings) { }
    public void Start() { }
    public void Stop() { }
}
