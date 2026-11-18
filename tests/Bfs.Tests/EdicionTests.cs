using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;
using Microsoft.Data.Sqlite;

namespace Bfs.Tests;

/// <summary>Edición Estándar: una empresa, pesaje normal y aduana, local; sin API para otros sistemas ni nube.</summary>
public class EdicionTests
{
    [Fact]
    public void La_configuracion_no_agranda_la_edicion()
    {
        // Los tests corren con la Completa compilada: la configuración puede bajarla a Estándar, nunca al revés.
        Assert.Equal(Edicion.Completa, Ediciones.Compilada);
        Assert.Equal(Edicion.Estandar, Ediciones.Efectiva(Edicion.Estandar));
        Assert.Equal("bfs-4-estandar", Ediciones.CarpetaDatos(Edicion.Estandar));
        Assert.NotEqual(Ediciones.UrlLocal(Edicion.Completa), Ediciones.UrlLocal(Edicion.Estandar));
    }

    [Fact]
    public async Task Estandar_una_empresa_sin_api_ni_nube()
    {
        await using var api = await TestApi.Iniciar(extra: "--Bfs:Edicion=Estandar");
        using var http = api.Cliente();
        var health = await http.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("Estandar", health.GetProperty("edicion").GetString());

        using var vendor = await api.LoginVendor();
        Assert.Equal(HttpStatusCode.NotFound, (await vendor.GetAsync("/api/integraciones")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vendor.GetAsync("/api/nube")).StatusCode);

        var empresas = await vendor.GetFromJsonAsync<JsonElement>("/api/empresas");
        Assert.Equal(1, empresas.GetArrayLength());
        var otra = await vendor.PostAsJsonAsync("/api/empresas", new { nombre = "Otra SA" });
        Assert.Equal(HttpStatusCode.BadRequest, otra.StatusCode);

        // Aunque alguien deje WORMS encendido en la configuración, la Estándar no encola nada.
        await api.Configurar(new() { [ConfigKeys.WormsEnabled] = "true", [ConfigKeys.WormsEndpoint] = "http://127.0.0.1:9/w", [ConfigKeys.WormsApiKey] = "k" });

        // Pesaje normal completo.
        using var admin = await api.LoginAdmin();
        var entrada = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000 });
        Assert.True(entrada.IsSuccessStatusCode, await entrada.Content.ReadAsStringAsync());
        var id = (await entrada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var salida = await admin.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 });
        Assert.Equal(18000, (await salida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("neto").GetInt32());

        using var conn = new SqliteConnection($"Data Source={Path.Combine(api.DataPath, "bfs.db")};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Outbox";
        Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
    }
}
