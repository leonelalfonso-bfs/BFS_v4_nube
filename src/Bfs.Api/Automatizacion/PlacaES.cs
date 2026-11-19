using System.Net.Sockets;
using Bfs.Core;

namespace Bfs.Api.Automatizacion;

/// <summary>La placa de entradas / salidas (8 y 8). Puntos numerados del 1 al 8 como en la 3.x.</summary>
public interface IPlacaES : IDisposable
{
    Task ConectarAsync(CancellationToken ct);
    bool Conectada { get; }
    Task<bool[]> LeerEntradasAsync(CancellationToken ct);
    Task<bool[]> LeerSalidasAsync(CancellationToken ct);
    Task EscribirSalidaAsync(int punto, bool valor, CancellationToken ct);
}

/// <summary>
/// Cliente Modbus TCP mínimo: leer entradas discretas (función 2), leer bobinas (1) y escribir una bobina (5).
/// Es lo único que usa la 3.x (<c>ModbusClient.java</c>); no hace falta una librería.
/// </summary>
public sealed class PlacaModbus(string ip, int puerto, byte unidad) : IPlacaES
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _uno = new(1, 1);
    private TcpClient? _tcp;
    private NetworkStream? _red;
    private ushort _transaccion;

    public bool Conectada => _tcp?.Connected == true;

    public async Task ConectarAsync(CancellationToken ct)
    {
        Dispose();
        var tcp = new TcpClient { NoDelay = true };
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(Espera);
        await tcp.ConnectAsync(ip, puerto, limite.Token);
        _tcp = tcp;
        _red = tcp.GetStream();
    }

    public async Task<bool[]> LeerEntradasAsync(CancellationToken ct) => Bits(await Pedir(0x02, 0, ConfigAutomatizacion.PuntosPlaca, ct));

    public async Task<bool[]> LeerSalidasAsync(CancellationToken ct) => Bits(await Pedir(0x01, 0, ConfigAutomatizacion.PuntosPlaca, ct));

    public Task EscribirSalidaAsync(int punto, bool valor, CancellationToken ct) =>
        Pedir(0x05, (ushort)(punto - 1), valor ? (ushort)0xFF00 : (ushort)0x0000, ct);

    /// <summary>Arma la trama (MBAP + función + dirección + dato) y devuelve la respuesta desde el byte de función.</summary>
    private async Task<byte[]> Pedir(byte funcion, ushort direccion, ushort dato, CancellationToken ct)
    {
        await _uno.WaitAsync(ct);
        try
        {
            var red = _red ?? throw new IOException("La placa no está conectada");
            var id = ++_transaccion;
            byte[] pedido =
            [
                (byte)(id >> 8), (byte)id, 0, 0, 0, 6, unidad,
                funcion, (byte)(direccion >> 8), (byte)direccion, (byte)(dato >> 8), (byte)dato
            ];
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(Espera);
            await red.WriteAsync(pedido, limite.Token);

            var cabecera = await Leer(red, 7, limite.Token);
            var largo = (cabecera[4] << 8) | cabecera[5];
            if (largo is < 2 or > 260) throw new IOException("Respuesta Modbus inválida");
            var cuerpo = await Leer(red, largo - 1, limite.Token);
            if ((cuerpo[0] & 0x80) != 0) throw new IOException($"La placa respondió error Modbus {cuerpo[1]} (función {funcion})");
            return cuerpo;
        }
        catch
        {
            // Una respuesta a medias deja la conexión desfasada: se cierra y se reconecta.
            Dispose();
            throw;
        }
        finally
        {
            _uno.Release();
        }
    }

    private static async Task<byte[]> Leer(NetworkStream red, int cantidad, CancellationToken ct)
    {
        var buffer = new byte[cantidad];
        await red.ReadExactlyAsync(buffer, ct);
        return buffer;
    }

    /// <summary>Función + cantidad de bytes + bits (el primero es el punto 1).</summary>
    private static bool[] Bits(byte[] r)
    {
        var bits = new bool[ConfigAutomatizacion.PuntosPlaca];
        for (var i = 0; i < bits.Length && 2 + i / 8 < r.Length; i++) bits[i] = (r[2 + i / 8] & (1 << (i % 8))) != 0;
        return bits;
    }

    public void Dispose()
    {
        _red?.Dispose();
        _tcp?.Dispose();
        _red = null;
        _tcp = null;
    }
}

/// <summary>Placa en memoria para probar sin hardware: las entradas se cambian desde Configuración → Automatización.</summary>
public sealed class PlacaSimulada : IPlacaES
{
    // Arranca con los sensores en verde (nadie los corta) para que se pueda pesar sin tocar nada.
    public bool[] Entradas { get; } = [true, true, true, true, false, false, false, false];
    public bool[] Salidas { get; } = new bool[ConfigAutomatizacion.PuntosPlaca];
    public bool Conectada { get; private set; }

    public Task ConectarAsync(CancellationToken ct)
    {
        Conectada = true;
        return Task.CompletedTask;
    }

    public Task<bool[]> LeerEntradasAsync(CancellationToken ct) => Task.FromResult((bool[])Entradas.Clone());
    public Task<bool[]> LeerSalidasAsync(CancellationToken ct) => Task.FromResult((bool[])Salidas.Clone());

    public Task EscribirSalidaAsync(int punto, bool valor, CancellationToken ct)
    {
        Salidas[punto - 1] = valor;
        return Task.CompletedTask;
    }

    public void Dispose() => Conectada = false;
}
