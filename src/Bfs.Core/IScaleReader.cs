namespace Bfs.Core;

public record ScaleReading(int Kilogramos, string Puerto, string Estado, DateTimeOffset En);

public interface IScaleReader
{
    ScaleReading Current { get; }
    event Action<ScaleReading>? WeightChanged;
    void Start();
    void Stop();
}
