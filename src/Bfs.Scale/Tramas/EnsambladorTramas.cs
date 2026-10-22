using System.Text;
using Bfs.Core;

namespace Bfs.Scale.Tramas;

/// <summary>
/// Arma tramas a partir de los bytes que llegan del puerto, que pueden venir cortados en cualquier lugar.
/// Misma regla que <c>SerialReader</c> de la 3.x para que las configuraciones existentes sigan sirviendo.
/// </summary>
public sealed class EnsambladorTramas
{
    public const int LargoMaximo = 200;

    /// <summary>En STX, si el indicador nunca manda STX, cada tanto se procesa lo acumulado (igual que la 3.x).</summary>
    public const int LargoSinStx = 20;

    private readonly bool _stx;
    private readonly StringBuilder _buffer = new();
    private bool _enTrama;

    /// <summary>En Line, después de una línea demasiado larga se descarta hasta el próximo salto para volver a sincronizar.</summary>
    private bool _descartando;

    public EnsambladorTramas(string protocolo)
    {
        _stx = string.Equals(protocolo, ProtocolosBalanza.Stx, StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerable<string> Agregar(ReadOnlySpan<byte> datos)
    {
        var tramas = new List<string>();
        foreach (var b in datos)
        {
            var trama = _stx ? Stx(b) : Linea(b);
            if (trama != null) tramas.Add(trama);
            if (_buffer.Length > LargoMaximo)
            {
                _buffer.Clear();
                _enTrama = false;
                _descartando = !_stx;
            }
        }
        return tramas;
    }

    private string? Stx(byte b)
    {
        switch (b)
        {
            case 0x02:
                _buffer.Clear();
                _enTrama = true;
                return null;
            case 0x03:
                var trama = _enTrama ? _buffer.ToString() : null;
                _enTrama = false;
                _buffer.Clear();
                return string.IsNullOrEmpty(trama) ? null : trama;
        }

        _buffer.Append((char)b);
        if (!_enTrama && _buffer.Length > LargoSinStx)
        {
            var sinStx = _buffer.ToString();
            _buffer.Clear();
            return sinStx;
        }
        return null;
    }

    private string? Linea(byte b)
    {
        if (b is (byte)'\r' or (byte)'\n')
        {
            if (_descartando)
            {
                _descartando = false;
                return null;
            }
            var trama = _buffer.ToString();
            _buffer.Clear();
            return string.IsNullOrWhiteSpace(trama) ? null : trama;
        }
        if (!_descartando) _buffer.Append((char)b);
        return null;
    }
}
