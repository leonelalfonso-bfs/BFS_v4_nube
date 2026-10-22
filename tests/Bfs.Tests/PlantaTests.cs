using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bfs.Tests;

public class PlantaTests
{
    [Fact]
    public async Task Admin_edita_datos_de_empresa_pero_no_crea_empresas()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        var res = await admin.PutAsJsonAsync($"/api/empresas/{TestApi.Empresa1}", new
        {
            nombre = "Acopio Norte SA",
            cuit = "20123456786",
            direccion = "Ruta 11 km 5",
            localidad = "Barranqueras",
            provincia = "Chaco",
            telefono = "",
            email = "admin@acopio.com"
        });
        res.EnsureSuccessStatusCode();
        var empresa = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("20-12345678-6", empresa.GetProperty("cuit").GetString());
        Assert.Equal("Chaco", empresa.GetProperty("provincia").GetString());
        Assert.Equal(JsonValueKind.Null, empresa.GetProperty("telefono").ValueKind);

        Assert.Equal(HttpStatusCode.Conflict,
            (await admin.PutAsJsonAsync($"/api/empresas/{TestApi.Empresa1}", new { nombre = "empresa 2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/empresas", new { nombre = "Nueva" })).StatusCode);

        using var vendor = await api.LoginVendor();
        (await vendor.PostAsJsonAsync("/api/empresas", new { nombre = "Nueva" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Aduana_y_clave_manual()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        (await admin.PutAsJsonAsync("/api/planta/aduana", new
        {
            codigo = "017",
            lot = "LOT-1",
            certificado1 = "C-123",
            certificado1Vence = "2027-03-31",
            certificado2 = "",
            certificado2Vence = (string?)null
        })).EnsureSuccessStatusCode();
        var aduana = await admin.GetFromJsonAsync<JsonElement>("/api/planta/aduana");
        Assert.Equal("017", aduana.GetProperty("codigo").GetString());
        Assert.Equal("2027-03-31", aduana.GetProperty("certificado1Vence").GetString());
        Assert.Equal(JsonValueKind.Null, aduana.GetProperty("certificado2Vence").ValueKind);

        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/funciones")).GetProperty("claveManual").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/planta/clave-manual", new { clave = "12" })).StatusCode);
        (await admin.PutAsJsonAsync("/api/planta/clave-manual", new { clave = "1234" })).EnsureSuccessStatusCode();
        Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/funciones")).GetProperty("claveManual").GetBoolean());

        // Ni BFS ve la clave: queda solo el hash, enmascarado.
        using var vendor = await api.LoginVendor();
        var config = await vendor.GetFromJsonAsync<Dictionary<string, string>>("/api/config");
        Assert.Equal("********", config!["clave_manual_hash"]);

        (await admin.PutAsJsonAsync("/api/planta/clave-manual", new { clave = "" })).EnsureSuccessStatusCode();
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/funciones")).GetProperty("claveManual").GetBoolean());
    }

    [Fact]
    public async Task Supervisor_no_toca_datos_de_planta()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "sup@planta.com", password = "clave123", nombre = "S", rol = "supervisor" })).EnsureSuccessStatusCode();
        using var sup = await api.Login("sup@planta.com", "clave123");

        Assert.Equal(HttpStatusCode.Forbidden, (await sup.GetAsync("/api/planta/aduana")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PutAsJsonAsync("/api/planta/clave-manual", new { clave = "1234" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.PutAsJsonAsync($"/api/empresas/{TestApi.Empresa1}", new { nombre = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sup.GetAsync("/api/funciones")).StatusCode);
    }

    [Fact]
    public async Task La_patente_recuerda_los_datos_de_la_ultima_entrada()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/vehiculos/AB123CD")).StatusCode);

        await Entrada(admin, new { empresaId = TestApi.Empresa1, patenteChasis = "ab 123 cd", patenteAcoplado = "ac 999 zz", conductor = "Juan Pérez", documento = "30111222", transporte = "Trans SA", taraPallets = 0 });
        // Una entrada sin chofer no borra el que ya estaba.
        await Entrada(admin, new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", conductor = "", taraPallets = 0 });

        var r = await admin.GetFromJsonAsync<JsonElement>("/api/vehiculos/ab-123-cd");
        var datos = r.GetProperty("datos");
        Assert.Equal("Juan Pérez", datos.GetProperty("conductor").GetString());
        Assert.Equal("AC999ZZ", datos.GetProperty("patenteAcoplado").GetString());
        Assert.Equal("Trans SA", datos.GetProperty("transporte").GetString());

        // Con tara conocida cargada, también la devuelve.
        (await admin.PostAsJsonAsync("/api/catalogos/taras", new { identificador = "AB123CD", kilogramos = 14000 })).EnsureSuccessStatusCode();
        r = await admin.GetFromJsonAsync<JsonElement>("/api/vehiculos/AB123CD");
        Assert.Equal(14000, r.GetProperty("tara").GetProperty("kilogramos").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = " - ", taraPallets = 0 })).StatusCode);
    }

    private static async Task Entrada(HttpClient http, object body) =>
        (await http.PostAsJsonAsync("/api/pesadas/entrada", body)).EnsureSuccessStatusCode();
}
