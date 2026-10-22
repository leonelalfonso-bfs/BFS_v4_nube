using Bfs.Core;

namespace Bfs.Scale;

public sealed class SimulatedScaleReader : IScaleReader
{
    private readonly PeriodicTimer _timer = new(TimeSpan.FromMilliseconds(400));
    private CancellationTokenSource? _cts;
    private int _base = 18240;

    public ScaleReading Current { get; private set; } = new(0, "SIM", "simulada", DateTimeOffset.Now);

    public event Action<ScaleReading>? WeightChanged;

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private async Task LoopAsync(CancellationToken token)
    {
        var random = new Random();
        try
        {
            while (await _timer.WaitForNextTickAsync(token))
            {
                if (random.NextDouble() < 0.08)
                {
                    _base = random.Next(8000, 45000);
                }

                var kg = _base + random.Next(-8, 9);
                Current = new ScaleReading(kg, "SIM", "simulada", DateTimeOffset.Now);
                WeightChanged?.Invoke(Current);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
