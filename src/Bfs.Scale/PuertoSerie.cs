using System.IO.Ports;
using Bfs.Core;

namespace Bfs.Scale;

/// <summary>Abre el puerto de una balanza. Está separado para poder probar el lector sin hardware.</summary>
public interface IPuertoSerieFactory
{
    IReadOnlyList<string> Puertos();

    /// <summary>Devuelve el flujo de lectura; cerrarlo cierra el puerto. Lanza si no se puede abrir.</summary>
    Stream Abrir(BalanzaConfig config);
}

public sealed class PuertoSerieFactory : IPuertoSerieFactory
{
    public IReadOnlyList<string> Puertos() => SerialPort.GetPortNames().Distinct().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

    public Stream Abrir(BalanzaConfig c)
    {
        var parity = c.Paridad.ToLowerInvariant() switch
        {
            "even" => Parity.Even,
            "odd" => Parity.Odd,
            "mark" => Parity.Mark,
            "space" => Parity.Space,
            _ => Parity.None
        };
        var stop = c.StopBits == 2 ? StopBits.Two : StopBits.One;

        // Abrir primero y configurar después: con CH340 en Windows 11 configurar al abrir suele fallar
        // (SetCommState error 31). Es el mismo rodeo que hacía la 3.x con jSerialComm.
        var port = new SerialPort(c.Puerto) { Handshake = Handshake.None, DtrEnable = false, RtsEnable = false };
        try
        {
            port.Open();
        }
        catch
        {
            port.Dispose();
            port = new SerialPort(c.Puerto, c.Baudios, parity, c.DataBits, stop) { Handshake = Handshake.None };
            port.Open();
            return new PuertoStream(port);
        }

        try
        {
            port.BaudRate = c.Baudios;
            port.DataBits = c.DataBits;
            port.Parity = parity;
            port.StopBits = stop;
        }
        catch (IOException)
        {
            // El driver rechazó la configuración pero el puerto quedó abierto: se lee igual.
        }
        return new PuertoStream(port);
    }

    /// <summary>El BaseStream del SerialPort, que al cerrarse también libera el puerto.</summary>
    private sealed class PuertoStream(SerialPort port) : Stream
    {
        private readonly Stream _base = port.BaseStream;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _base.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => _base.ReadAsync(buffer, ct);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { port.Close(); } catch (IOException) { }
                port.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
