using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;

namespace Bfs.Tests;

/// <summary>La API real en modo Nodo, con carpeta de datos temporal y puerto libre.</summary>
public sealed class TestApi : IAsyncDisposable
{
    public const string ClaveSeed = "admin";
    public static readonly Guid Empresa1 = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly TempDir _tmp;
    private readonly WebApplication _app;

    private TestApi(TempDir tmp, WebApplication app)
    {
        _tmp = tmp;
        _app = app;
    }

    public string DataPath => _tmp.Path;

    /// <param name="sinLicencia">true = entorno Development con licencia desactivada; false = como una instalación real.</param>
    public static async Task<TestApi> Iniciar(bool sinLicencia = true, params string[] extra)
    {
        var tmp = new TempDir();
        var args = new List<string> { "--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={tmp.Path}" };
        args.Add(sinLicencia ? "--environment=Development" : "--environment=Production");
        if (sinLicencia) args.Add("--Bfs:Licencia:Requerida=false");
        args.AddRange(extra);

        var app = ApiHost.Create(args.ToArray());
        await app.StartAsync();
        return new TestApi(tmp, app);
    }

    public HttpClient Cliente() => new() { BaseAddress = new Uri(_app.Urls.First()) };

    public async Task<HttpClient> Login(string email, string password)
    {
        var http = Cliente();
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public Task<HttpClient> LoginAdmin() => Login(DbInitializer.SeedAdminEmail, ClaveSeed);
    public Task<HttpClient> LoginVendor() => Login(DbInitializer.SeedVendorEmail, "bfs-setup");

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _tmp.Dispose();
    }
}
