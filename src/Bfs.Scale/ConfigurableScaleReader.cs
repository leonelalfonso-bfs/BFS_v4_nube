using System.IO.Ports;
using Bfs.Core;

namespace Bfs.Scale;

public sealed class ConfigurableScaleReader : IScaleReader, IScaleController
{
    private IScaleReader _inner = new SimulatedScaleReader();
    private SerialScaleReader? _serial;

    public ScaleReading Current => _inner.Current;
    public string Status => Current.Estado;
    public string? LastError => _serial?.LastError;
    public ScaleSettings Settings { get; private set; } = ScaleSettings.Simulated();

    public event Action<ScaleReading>? WeightChanged;

    public IReadOnlyList<string> ListPorts() => SerialPort.GetPortNames().OrderBy(p => p).ToArray();

    public void Start() => _inner.Start();

    public void Stop()
    {
        _inner.WeightChanged -= Forward;
        _inner.Stop();
    }

    public void Apply(ScaleSettings settings)
    {
        Stop();
        Settings = settings;
        if (settings.Mode == "serial" && !string.IsNullOrWhiteSpace(settings.Port))
        {
            _serial = new SerialScaleReader(settings);
            _inner = _serial;
        }
        else
        {
            _serial = null;
            _inner = new SimulatedScaleReader();
        }
        _inner.WeightChanged += Forward;
        _inner.Start();
    }

    private void Forward(ScaleReading reading) => WeightChanged?.Invoke(reading);
}
