using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;

namespace Bfs.Tests;

public class CatalogosTests
{
    [Theory]
    [InlineData("20-12345678-6", "20-12345678-6")]
    [InlineData("20123456786", "20-12345678-6")]
    [InlineData(" 20.12345678.6 ", "20-12345678-6")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Cuit_valido_se_normaliza(string? entrada, string? esperado)
    {
        var (valor, error) = Cuit.Normalizar(entrada);
        Assert.Null(error);
        Assert.Equal(esperado, valor);
    }

    [Theory]
    [InlineData("20-12345678-5")]
    [InlineData("2012345678")]
    [InlineData("20-1234567A-6")]
    public void Cuit_invalido_da_error(string entrada) => Assert.NotNull(Cuit.Normalizar(entrada).Error);

    [Theory]
    [InlineData("ab 123-cd", "AB123CD")]
    [InlineData(" aa111aa ", "AA111AA")]
    [InlineData(null, "")]
    public void Patente_se_normaliza(string? entrada, string esperado) => Assert.Equal(esperado, Patente.Normalizar(entrada));

    [Fact]
    public async Task Crud_de_clientes_con_busqueda_duplicados_y_borrado_logico()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        var agro = await Ok(admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = " Agro SA ", cuit = "20123456786" }));
        Assert.Equal("Agro SA", agro.GetProperty("razonSocial").GetString());
        Assert.Equal("20-12345678-6", agro.GetProperty("cuit").GetString());
        await Ok(admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Molinos del Sur" }));

        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "AGRO sa" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "X", cuit = "20-12345678-5" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "  " })).StatusCode);

        var busqueda = await admin.GetFromJsonAsync<JsonElement>("/api/catalogos/clientes?q=MOLI");
        Assert.Equal(1, busqueda.GetArrayLength());

        var id = agro.GetProperty("id").GetGuid();
        var editado = await Ok(admin.PutAsJsonAsync($"/api/catalogos/clientes/{id}", new { razonSocial = "Agro S.A.", cuit = "" }));
        Assert.Equal(JsonValueKind.Null, editado.GetProperty("cuit").ValueKind);
        Assert.Equal(2, editado.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/catalogos/clientes/{id}", new { razonSocial = "molinos del sur" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/catalogos/clientes/{id}")).StatusCode);
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>("/api/catalogos/clientes")).GetArrayLength());

        // Darlo de alta de nuevo recupera el mismo registro (mismo Id): las pesadas viejas siguen asociadas.
        var recuperado = await Ok(admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "agro s.a." }));
        Assert.Equal(id, recuperado.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task No_se_borra_un_cliente_con_usuarios_de_portal()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var id = (await Ok(admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Agro SA" }))).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "c@agro.com", password = "clave123", nombre = "C", rol = "cliente", clienteId = id })).EnsureSuccessStatusCode();

        var res = await admin.DeleteAsync($"/api/catalogos/clientes/{id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("usuarios", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Transportes_exportadores_productos_y_taras()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/transportes", new { razonSocial = "Trans", esAta = true })).StatusCode);
        var ata = await Ok(admin.PostAsJsonAsync("/api/catalogos/transportes", new { razonSocial = "Trans", esAta = true, codigoAta = " ATA-9 " }));
        Assert.Equal("ATA-9", ata.GetProperty("codigoAta").GetString());

        await Ok(admin.PostAsJsonAsync("/api/catalogos/exportadores", new { razonSocial = "Export SA", cuit = "20-12345678-6" }));
        var prod = await Ok(admin.PostAsJsonAsync("/api/catalogos/productos", new { nombre = "Soja", codigo = "S1" }));
        Assert.Equal("S1", prod.GetProperty("codigo").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/catalogos/taras", new { identificador = "AA111AA", kilogramos = 0 })).StatusCode);
        var tara = await Ok(admin.PostAsJsonAsync("/api/catalogos/taras", new { identificador = "aa 111 aa", kilogramos = 14500, descripcion = "Batea" }));
        Assert.Equal("AA111AA", tara.GetProperty("identificador").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/catalogos/taras", new { identificador = "AA-111-AA", kilogramos = 1 })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/catalogos/inexistente")).StatusCode);
    }

    [Fact]
    public async Task Operador_ve_catalogos_pero_no_los_edita()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var id = (await Ok(admin.PostAsJsonAsync("/api/catalogos/productos", new { nombre = "Maiz" }))).GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "op@planta.com", password = "clave123", nombre = "Op", rol = "operador" })).EnsureSuccessStatusCode();
        using var op = await api.Login("op@planta.com", "clave123");

        Assert.Equal(1, (await op.GetFromJsonAsync<JsonElement>("/api/catalogos/productos")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PutAsJsonAsync($"/api/catalogos/productos/{id}", new { nombre = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.DeleteAsync($"/api/catalogos/productos/{id}")).StatusCode);
    }

    private static async Task<JsonElement> Ok(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }
}
