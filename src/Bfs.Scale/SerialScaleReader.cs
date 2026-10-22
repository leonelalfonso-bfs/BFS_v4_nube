using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Bfs.Core;

namespace Bfs.Scale;

public sealed class SerialScaleReader : IScaleReader
{
    private readonly ScaleSettings _settings;
    private SerialPort? _port;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _buffer = new();
    private bool _inFrame;

    public SerialScaleReader(ScaleSettings settings)
    {
        _settings = settings;
        Current = new ScaleReading(0, settings.Port, "cerrada", DateTimeOffset.Now);
    }

    public ScaleReading Current { get; private set; }
    public string? LastError { get; private set; }
    public event Action<ScaleReading>? WeightChanged;

    public void Start()
    {
        if (_cts != null) return;
        _cts = new CancellationTokenSource();
        try
        {
            _port = OpenPort(_settings);
            LastError = null;
            Current = Current with { Estado = "abierta", Puerto = _settings.Port };
            WeightChanged?.Invoke(Current);
            _ = ReadLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Current = new ScaleReading(0, _settings.Port, "error", DateTimeOffset.Now);
            WeightChanged?.Invoke(Current);
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        try { _port?.Close(); } catch { }
        _port?.Dispose();
        _port = null;
    }

    private static SerialPort OpenPort(ScaleSettings s)
    {
        var parity = s.Parity.ToLowerInvariant() switch
        {
            "even" => Parity.Even,
            "odd" => Parity.Odd,
            "mark" => Parity.Mark,
            "space" => Parity.Space,
            _ => Parity.None
        };
        var stop = s.StopBits == 2 ? StopBits.Two : StopBits.One;
        var port = new SerialPort(s.Port)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false
        };

        try
        {
            port.Open();
        }
        catch
        {
            port.Dispose();
            port = new SerialPort(s.Port, s.Baud, parity, s.DataBits, stop)
            {
                ReadTimeout = 1000,
                Handshake = Handshake.None
            };
            port.Open();
            return port;
        }

        try
        {
            port.BaudRate = s.Baud;
            port.DataBits = s.DataBits;
            port.Parity = parity;
            port.StopBits = stop;
        }
        catch
        {
            // CH340 Win11: a veces abre pero rechaza SetCommState. Seguimos leyendo igual.
        }

        return port;
    }

    private async Task ReadLoop(CancellationToken token)
    {
        var port = _port;
        if (port == null) return;
        var raw = new byte[256];
        try
        {
            while (!token.IsCancellationRequested && port.IsOpen)
            {
                int n;
                try
                {
                    n = await port.BaseStream.ReadAsync(raw.AsMemory(0, raw.Length), token);
                }
                catch (OperationCanceledException) { break; }
                catch (TimeoutException) { continue; }
                catch (IOException ex)
                {
                    LastError = ex.Message;
                    Current = Current with { Estado = "error" };
                    WeightChanged?.Invoke(Current);
                    await Task.Delay(1000, token);
                    continue;
                }

                if (n <= 0) continue;
                for (var i = 0; i < n; i++)
                {
                    Consume(raw[i]);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Consume(byte data)
    {
        if (_settings.Protocol.Equals("STX", StringComparison.OrdinalIgnoreCase))
        {
            if (data == 2)
            {
                _buffer.Clear();
                _inFrame = true;
                return;
            }
            if (data == 3)
            {
                if (_inFrame) Publish(_buffer.ToString());
                _inFrame = false;
                _buffer.Clear();
                return;
            }
            if (_inFrame) _buffer.Append((char)data);
            return;
        }

        if (data is (byte)'\n' or (byte)'\r')
        {
            if (_buffer.Length > 0)
            {
                Publish(_buffer.ToString());
                _buffer.Clear();
            }
            return;
        }

        if (data >= 32) _buffer.Append((char)data);
        if (_buffer.Length > 200) _buffer.Clear();
    }

    private void Publish(string frame)
    {
        var kg = ExtractKg(frame, _settings.Extract);
        if (kg == null) return;
        Current = new ScaleReading(kg.Value, _settings.Port, "abierta", DateTimeOffset.Now);
        WeightChanged?.Invoke(Current);
    }

    internal static int? ExtractKg(string frame, string extract)
    {
        if (extract.Contains(':'))
        {
            var parts = extract.Split(':');
            if (int.TryParse(parts[0], out var start) && int.TryParse(parts[1], out var end) &&
                start >= 0 && end > start && end <= frame.Length)
            {
                var slice = Regex.Replace(frame[start..end], "[^0-9.-]", "");
                if (double.TryParse(slice, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                {
                    return (int)Math.Abs(v);
                }
            }
        }

        var match = Regex.Match(frame, @"-?\d+(?:[.,]\d+)?");
        if (match.Success && double.TryParse(match.Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n))
        {
            return (int)Math.Abs(n);
        }
        return null;
    }
}
