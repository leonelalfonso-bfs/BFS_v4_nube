using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api.Licencias;
using Bfs.Core;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Sincronizacion;

/// <summary>Conexión de esta portería con la nube (archivo <c>nube.json</c> en la carpeta de datos).</summary>
public class ConexionNube
{
    public string Url { get; set; } = "";
    public Guid NodoId { get; set; }
    public string Token { get; set; } = "";
    public string Tenant { get; set; } = "";

    /// <summary>Por tipo, hasta qué modificación local ya se mandó.</summary>
    public Dictionary<string, DateTimeOffset> Enviado { get; set; } = new();

    /// <summary>Por tipo, hasta qué secuencia de la nube ya se recibió.</summary>
    public Dictionary<string, long> Recibido { get; set; } = new();

    public DateTimeOffset? UltimaSincronizacion { get; set; }
    public string? UltimoError { get; set; }
    public List<string> Conflictos { get; set; } = [];
}

public record EstadoSync(bool Conectado, string? Url, string? Tenant, DateTimeOffset? UltimaSincronizacion, string? UltimoError, int Pendientes, IReadOnlyList<string> Conflictos);

/// <summary>
/// Sincroniza la portería con la nube cada minuto. Sin internet la portería sigue pesando: lo pendiente sale en la
/// próxima vuelta que conecte. La nube renueva la licencia (15 días) en cada sincronización.
/// </summary>
public sealed class SincronizadorNodo(IServiceScopeFactory scopes, BfsPaths paths, IHttpClientFactory http, LicenciaService licencias,
    ILogger<SincronizadorNodo> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _uno = new(1, 1);
    private readonly string _archivo = Path.Combine(paths.Root, "nube.json");

    public ConexionNube? Conexion()
    {
        if (!File.Exists(_archivo)) return null;
        try { return JsonSerializer.Deserialize<ConexionNube>(File.ReadAllText(_archivo)); }
        catch (JsonException) { return null; }
    }

    private void Guardar(ConexionNube? c)
    {
        if (c == null)
        {
            if (File.Exists(_archivo)) File.Delete(_archivo);
            return;
        }
        var temporal = _archivo + ".tmp";
        File.WriteAllText(temporal, JsonSerializer.Serialize(c, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporal, _archivo, overwrite: true);
    }

    /// <summary>Canjea el código que dio BFS por el token de esta portería y hace la primera sincronización.</summary>
    public async Task<string?> ActivarAsync(string url, string codigo)
    {
        url = url.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return "La dirección de la nube no es válida (ej. https://pesaje.bfs.com.ar)";
        try
        {
            using var cliente = Cliente(url);
            var res = await cliente.PostAsJsonAsync("/api/sync/activar", new PedidoActivacion(codigo, licencias.Estado().Maquina));
            if (!res.IsSuccessStatusCode) return (await res.Content.ReadAsStringAsync()).Trim('"') is { Length: > 0 } msg ? msg : $"La nube respondió {(int)res.StatusCode}";
            var r = (await res.Content.ReadFromJsonAsync<RespuestaActivacion>(AplicadorSync.Json))!;
            Guardar(new ConexionNube { Url = url, NodoId = r.NodoId, Token = r.Token, Tenant = r.Tenant });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return $"No se pudo conectar con {url}: {ex.Message}";
        }
        await SincronizarAsync();
        return null;
    }

    public void Desvincular() => Guardar(null);

    public async Task<EstadoSync> EstadoAsync()
    {
        var c = Conexion();
        if (c == null) return new EstadoSync(false, null, null, null, null, 0, []);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
        var pendientes = 0;
        foreach (var tipo in TiposSync.Subida)
        {
            var desde = c.Enviado.GetValueOrDefault(tipo, DateTimeOffset.MinValue);
            pendientes += await TiposSync.Consulta(db, tipo).CountAsync(x => x.ModificadoEn > desde);
        }
        return new EstadoSync(true, c.Url, c.Tenant, c.UltimaSincronizacion, c.UltimoError, pendientes, c.Conflictos);
    }

    /// <returns>El error, o null si sincronizó.</returns>
    public async Task<string?> SincronizarAsync(CancellationToken ct = default)
    {
        var c = Conexion();
        if (c == null) return "Esta portería no está conectada a la nube";
        await _uno.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
            var inicio = DateTimeOffset.UtcNow;

            // Lo que cambió acá desde el último envío (con un poco de solapamiento, por las dudas).
            var cambios = new Dictionary<string, List<JsonElement>>();
            var enviadoHasta = new Dictionary<string, DateTimeOffset>();
            foreach (var tipo in TiposSync.Subida)
            {
                var desde = c.Enviado.TryGetValue(tipo, out var d) ? d - SecretosNodo.Solapamiento : DateTimeOffset.MinValue;
                var filas = await TiposSync.Consulta(db, tipo).AsNoTracking()
                    .Where(x => x.ModificadoEn > desde).OrderBy(x => x.ModificadoEn).Take(SecretosNodo.Lote).ToListAsync(ct);
                if (filas.Count == 0) continue;
                cambios[tipo] = filas.Select(f => JsonSerializer.SerializeToElement(f, TiposSync.Clr(tipo), AplicadorSync.Json)).ToList();
                enviadoHasta[tipo] = filas[^1].ModificadoEn;
            }

            using var cliente = Cliente(c.Url);
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Nodo", $"{c.NodoId}:{c.Token}");
            var res = await cliente.PostAsJsonAsync("/api/sync", new PedidoSync(licencias.Estado().Maquina, cambios, c.Recibido), AplicadorSync.Json, ct);
            if (!res.IsSuccessStatusCode)
            {
                var detalle = (await res.Content.ReadAsStringAsync(ct)).Trim('"');
                return Fallo(c, res.StatusCode == HttpStatusCode.Unauthorized
                    ? "La nube no reconoce esta portería (¿se generó otro código?). Pedí un código nuevo a BFS."
                    : detalle.Length > 0 ? detalle : $"La nube respondió {(int)res.StatusCode}");
            }
            var r = (await res.Content.ReadFromJsonAsync<RespuestaSync>(AplicadorSync.Json, ct))!;

            // Lo que llega de la nube (incluidos los usuarios y las anulaciones hechas desde la oficina).
            var conflictos = r.Aplicados.Values.SelectMany(a => a.Conflictos).ToList();
            var recibidoHasta = new Dictionary<string, DateTimeOffset>();
            foreach (var tipo in TiposSync.Bajada)
            {
                if (r.Cambios.TryGetValue(tipo, out var filas) && filas.Count > 0)
                {
                    conflictos.AddRange((await AplicadorSync.AplicarAsync(db, tipo, filas, esNube: false)).Conflictos);
                    recibidoHasta[tipo] = filas.Max(f => f.GetProperty("modificadoEn").GetDateTimeOffset());
                }
            }

            foreach (var (tipo, hasta) in enviadoHasta) c.Enviado[tipo] = hasta;
            // Lo que acaba de bajar de la nube ya está allá: no cuenta como pendiente de envío.
            // Solo se adelanta si este envío no quedó cortado por el lote, y nunca más allá del inicio de esta vuelta
            // (lo que se modificó acá mientras sincronizaba sale en la próxima).
            foreach (var (tipo, hasta) in recibidoHasta)
            {
                var enviadas = cambios.TryGetValue(tipo, out var l) ? l.Count : 0;
                if (enviadas >= SecretosNodo.Lote) continue;
                var tope = hasta < inicio ? hasta : inicio;
                if (!c.Enviado.TryGetValue(tipo, out var actual) || tope > actual) c.Enviado[tipo] = tope;
            }
            c.Recibido = r.Hasta;
            c.Tenant = r.Tenant;
            c.UltimaSincronizacion = DateTimeOffset.UtcNow;
            c.UltimoError = null;
            c.Conflictos = conflictos.TakeLast(50).ToList();
            Guardar(c);

            if (r.Licencia != null && licencias.Activar(r.Licencia) is { Estado: not EstadoLicencia.Valida } lic)
            {
                log.LogWarning("La licencia que mandó la nube no es válida para esta PC: {Mensaje}", lic.Mensaje);
            }
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return Fallo(c, "Sin conexión con la nube: " + ex.Message);
        }
        finally
        {
            _uno.Release();
        }
    }

    private string Fallo(ConexionNube c, string error)
    {
        c.UltimoError = error;
        Guardar(c);
        return error;
    }

    private HttpClient Cliente(string url)
    {
        var cliente = http.CreateClient("nube");
        cliente.BaseAddress = new Uri(url);
        cliente.Timeout = TimeSpan.FromSeconds(60);
        return cliente;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (Conexion() != null)
            {
                try
                {
                    if (await SincronizarAsync(stoppingToken) is { } error) log.LogDebug("Sync: {Error}", error);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    log.LogError(ex, "Error al sincronizar con la nube");
                }
            }
            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
