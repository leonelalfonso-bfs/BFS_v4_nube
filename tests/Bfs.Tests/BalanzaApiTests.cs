using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;
using Bfs.Infrastructure.Data;

namespace Bfs.Tests;

public class BalanzaApiTests
{
    [Fact]
    public async Task Base_nueva_trae_una_balanza_simulada_y_los_modelos_de_la_3x()
    {
        await using var api = await TestApi.Iniciar();
        using var vendor = await api.LoginVendor();

        var modelos = await vendor.GetFromJsonAsync<JsonElement>("/api/balanzas/modelos");
        Assert.Equal(DbInitializer.ModelosDe3x.Count, modelos.GetArrayLength());
        var moretti = modelos.EnumerateArray().Single(m => m.GetProperty("nombre").GetString() == "Moretti W-180");
        Assert.Equal("5:10", moretti.GetProperty("extraccion").GetString());
        Assert.Equal(7, moretti.GetProperty("dataBits").GetInt32());

        var lecturas = await Esperar(vendor, l => l.GetArrayLength() == 1 && l[0].GetProperty("kilogramos").GetInt32() > 0);
        Assert.Equal("Balanza 1", lecturas[0].GetProperty("nombre").GetString());
        Assert.Equal("simulada", lecturas[0].GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Alta_de_balanza_valida_parametros_y_puerto_repetido()
    {
        await using var api = await TestApi.Iniciar();
        using var vendor = await api.LoginVendor();

        Assert.Equal(HttpStatusCode.BadRequest, (await Alta(vendor, "B2", "COM98", extraccion: "10:5")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Alta(vendor, "B2", "COM98", baudios: 1234)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Alta(vendor, "B2", "")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Alta(vendor, "balanza 1", "COM98")).StatusCode);

        (await Alta(vendor, "Salida", "com98")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Alta(vendor, "Otra", "COM98")).StatusCode);

        // COM98 no existe: queda en error y el pesaje no toma peso de ella.
        var lecturas = await Esperar(vendor, l => l.EnumerateArray().Any(x => x.GetProperty("nombre").GetString() == "Salida" && x.GetProperty("estado").GetString() == "error"));
        var salida = lecturas.EnumerateArray().Single(x => x.GetProperty("nombre").GetString() == "Salida");
        var res = await vendor.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AA111AA", taraPallets = 0, balanzaId = salida.GetProperty("balanzaId").GetGuid()
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("no está leyendo", await res.Content.ReadAsStringAsync());

        // Con la simulada sí, y la pesada queda con el nombre de la balanza.
        var entrada = await vendor.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AA111AA", taraPallets = 0 });
        entrada.EnsureSuccessStatusCode();
        Assert.Equal("Balanza 1", (await entrada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balanza").GetString());
    }

    [Fact]
    public async Task Probar_extraccion_sobre_una_trama_pegada()
    {
        await using var api = await TestApi.Iniciar();
        using var vendor = await api.LoginVendor();

        var r = await vendor.PostAsJsonAsync("/api/balanzas/probar", new { trama = @"\x02ST,GS,012480,kg\x03", extraccion = "7:13" });
        r.EnsureSuccessStatusCode();
        Assert.Equal(12480, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("kilogramos").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await vendor.PostAsJsonAsync("/api/balanzas/probar", new { trama = "x", extraccion = "nada" })).StatusCode);
    }

    [Fact]
    public async Task Solo_bfs_configura_balanzas_el_operador_lee()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/balanzas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Alta(admin, "X", "COM5")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/balanzas/lecturas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/scale/peso")).StatusCode);
    }

    private static Task<HttpResponseMessage> Alta(HttpClient http, string nombre, string puerto, int baudios = 9600, string? extraccion = null) =>
        http.PostAsJsonAsync("/api/balanzas", new
        {
            nombre, modo = ModosBalanza.Serie, puerto, baudios, dataBits = 8, paridad = "None", stopBits = 1,
            protocolo = ProtocolosBalanza.Stx, extraccion
        });

    private static async Task<JsonElement> Esperar(HttpClient http, Func<JsonElement, bool> condicion)
    {
        var hasta = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var l = await http.GetFromJsonAsync<JsonElement>("/api/balanzas/lecturas");
            if (condicion(l)) return l;
            if (DateTime.UtcNow > hasta) Assert.Fail("Las lecturas no llegaron al estado esperado: " + l);
            await Task.Delay(100);
        }
    }
}
