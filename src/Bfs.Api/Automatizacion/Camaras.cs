using System.Collections.Concurrent;
using System.Net;
using Bfs.Core;
using Microsoft.Extensions.FileProviders;

namespace Bfs.Api.Automatizacion;

/// <summary>Una foto tal como la mandó la cámara.</summary>
public record FotoCamara(string Camara, byte[] Datos, string Tipo);

/// <summary>
/// Cámaras IP por snapshot HTTP (Dahua: /cgi-bin/snapshot.cgi, Hikvision: /ISAPI/Streaming/channels/101/picture).
/// La vista en vivo de la pantalla pide la foto varias veces por segundo; para que dos pantallas no le pidan el doble
/// a la cámara, la última foto se reutiliza durante <see cref="Reuso"/>. Las cámaras solo se tocan si la automatización
/// está habilitada.
/// </summary>
public sealed class CamarasService(AutomatizacionService automatizacion)
{
    public static readonly TimeSpan Reuso = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<string, HttpClient> _clientes = new();
    private readonly ConcurrentDictionary<int, (DateTimeOffset En, Task<FotoCamara?> Foto)> _ultimas = new();
    private static readonly Lazy<byte[]> FotoSimulada = new(() =>
    {
        var archivo = new ManifestEmbeddedFileProvider(typeof(ApiHost).Assembly, "wwwroot").GetFileInfo("images/bfs_logo.png");
        using var s = archivo.CreateReadStream();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    });

    private List<CamaraAutomatizacion> Camaras() =>
        automatizacion.Config.Habilitada ? automatizacion.Config.Camaras.Where(c => c.Habilitada).ToList() : [];

    /// <summary>La foto actual de la cámara (índice 0 o 1 entre las habilitadas). Null si no responde.</summary>
    public Task<FotoCamara?> FotoAsync(int indice)
    {
        var camaras = Camaras();
        if (indice < 0 || indice >= camaras.Count) return Task.FromResult<FotoCamara?>(null);
        var ahora = DateTimeOffset.UtcNow;
        var actual = _ultimas.AddOrUpdate(indice,
            _ => (ahora, Pedir(camaras[indice])),
            (_, previa) => ahora - previa.En < Reuso ? previa : (ahora, Pedir(camaras[indice])));
        return actual.Foto;
    }

    /// <summary>Una foto de cada cámara habilitada, a la vez. Las que no responden se saltean.</summary>
    public async Task<List<FotoCamara>> FotosAsync()
    {
        var pedidos = Camaras().Select(Pedir).ToList();
        var fotos = await Task.WhenAll(pedidos);
        return fotos.Where(f => f != null).Select(f => f!).ToList();
    }

    /// <summary>Para "Probar" en Configuración: la cámara tal cual se cargó, aunque todavía no esté guardada.</summary>
    public Task<FotoCamara?> ProbarAsync(CamaraAutomatizacion camara) => Pedir(camara);

    private async Task<FotoCamara?> Pedir(CamaraAutomatizacion c)
    {
        if (c.Url.Trim().Equals("sim", StringComparison.OrdinalIgnoreCase)) return new FotoCamara(c.Nombre, FotoSimulada.Value, "image/png");
        try
        {
            using var cancelar = new CancellationTokenSource(Espera);
            var datos = await Cliente(c).GetByteArrayAsync(c.Url.Trim(), cancelar.Token);
            return datos.Length == 0 ? null : new FotoCamara(c.Nombre, datos, TipoImagen(datos));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Un cliente por cámara y usuario: Basic y Digest (Dahua usa Digest) los resuelve el propio HttpClient.</summary>
    private HttpClient Cliente(CamaraAutomatizacion c) =>
        _clientes.GetOrAdd($"{c.Url}|{c.Usuario}|{c.Clave}", _ =>
        {
            var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
            if (!string.IsNullOrEmpty(c.Usuario)) handler.Credentials = new NetworkCredential(c.Usuario, c.Clave ?? "");
            return new HttpClient(handler) { Timeout = Espera };
        });

    public static string TipoImagen(byte[] d) =>
        d.Length > 3 && d[0] == 0x89 && d[1] == 0x50 ? "image/png" : "image/jpeg";

    public static string Extension(string tipo) => tipo == "image/png" ? ".png" : ".jpg";
}

/// <summary>Guarda las fotos de una pesada en la carpeta de datos del puesto (fotos/aaaa/mm/).</summary>
public sealed class FotosPesadas(BfsPaths paths)
{
    public IReadOnlyList<FotoPesada> Guardar(Guid pesadaId, string momento, IReadOnlyList<FotoCamara> fotos)
    {
        if (fotos.Count == 0) return [];
        var ahora = DateTime.Now;
        var relativa = Path.Combine("fotos", ahora.ToString("yyyy"), ahora.ToString("MM"));
        Directory.CreateDirectory(Path.Combine(paths.Root, relativa));
        var guardadas = new List<FotoPesada>();
        for (var i = 0; i < fotos.Count; i++)
        {
            var archivo = Path.Combine(relativa, $"{pesadaId:N}-{momento}-{i + 1}{CamarasService.Extension(fotos[i].Tipo)}");
            File.WriteAllBytes(Path.Combine(paths.Root, archivo), fotos[i].Datos);
            guardadas.Add(new FotoPesada(momento, fotos[i].Camara, archivo.Replace('\\', '/')));
        }
        return guardadas;
    }

    /// <summary>La foto guardada, o null si no está en este puesto (por ejemplo, en la nube).</summary>
    public byte[]? Leer(FotoPesada foto)
    {
        var ruta = Path.GetFullPath(Path.Combine(paths.Root, foto.Archivo));
        // Solo dentro de la carpeta de fotos: el nombre viene de la base, pero nunca se sale de ahí.
        if (!ruta.StartsWith(Path.GetFullPath(Path.Combine(paths.Root, "fotos")), StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(ruta) ? File.ReadAllBytes(ruta) : null;
    }
}
