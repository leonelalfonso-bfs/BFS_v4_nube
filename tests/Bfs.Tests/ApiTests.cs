using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api;

namespace Bfs.Tests;

/// <summary>Levanta la API real en modo Nodo, con una carpeta de datos temporal y un puerto libre.</summary>
public class ApiTests
{
    private static readonly Guid Empresa1 = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Flujo_login_entrada_salida()
    {
        using var tmp = new TempDir();
        await using var app = ApiHost.Create(["--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={tmp.Path}"]);
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var health = await http.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("Nodo", health.GetProperty("modo").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/pesadas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await http.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "mal" })).StatusCode);

        var login = await http.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "admin" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var entrada = await http.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = Empresa1,
            patenteChasis = "AB 123 CD",
            pesoEntrada = 30000,
            taraPallets = 500
        });
        entrada.EnsureSuccessStatusCode();
        var pesada = await entrada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("1", pesada.GetProperty("ticketNumero").GetString());
        Assert.Equal("AB123CD", pesada.GetProperty("patenteChasis").GetString());
        Assert.Equal("admin", pesada.GetProperty("operador").GetString());
        Assert.Equal(1, pesada.GetProperty("version").GetInt64());
        var id = pesada.GetProperty("id").GetGuid();

        var salida = await http.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 });
        salida.EnsureSuccessStatusCode();
        var cerrada = await salida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SALIDA", cerrada.GetProperty("estado").GetString());
        Assert.Equal(17500, cerrada.GetProperty("neto").GetInt32());

        var repetida = await http.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 });
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await http.PostAsJsonAsync($"/api/pesadas/{Guid.NewGuid()}/salida", new { pesoSalida = 1 })).StatusCode);

        var lista = await http.GetFromJsonAsync<JsonElement>("/api/pesadas");
        Assert.Equal(1, lista.GetArrayLength());

        // El admin del cliente no puede tocar la configuración de BFS.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await http.PutAsJsonAsync("/api/config", new Dictionary<string, string> { ["aduana"] = "true" })).StatusCode);

        await app.StopAsync();
    }

    [Fact]
    public void Modo_nube_sin_cadena_de_conexion_falla_al_arrancar()
    {
        using var tmp = new TempDir();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiHost.Create(["--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={tmp.Path}", "--Bfs:Modo=Nube"]));
        Assert.Contains("ConnectionStrings:Bfs", ex.Message);
    }
}
