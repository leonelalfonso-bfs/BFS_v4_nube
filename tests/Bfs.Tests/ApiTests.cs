using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api;
using Bfs.Infrastructure.Data;

namespace Bfs.Tests;

public class ApiTests
{
    private static readonly Guid Empresa1 = TestApi.Empresa1;

    [Fact]
    public async Task Flujo_login_entrada_salida()
    {
        await using var api = await TestApi.Iniciar();
        using var anon = api.Cliente();

        var health = await anon.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("Nodo", health.GetProperty("modo").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/pesadas")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PostAsJsonAsync("/api/auth/login", new { email = DbInitializer.SeedAdminEmail, password = "mal" })).StatusCode);

        // El email no distingue mayúsculas ni espacios.
        using var http = await api.Login("  ADMIN@bfs.local ", TestApi.ClaveSeed);

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
        Assert.Equal(DbInitializer.SeedAdminEmail, pesada.GetProperty("operador").GetString());
        Assert.Equal(1, pesada.GetProperty("version").GetInt64());
        var id = pesada.GetProperty("id").GetGuid();

        var salida = await http.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 });
        salida.EnsureSuccessStatusCode();
        var cerrada = await salida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SALIDA", cerrada.GetProperty("estado").GetString());
        Assert.Equal(17500, cerrada.GetProperty("neto").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync($"/api/pesadas/{Guid.NewGuid()}/salida", new { pesoSalida = 1 })).StatusCode);
        Assert.Equal(1, (await http.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());

        // El admin del cliente no ve ni toca la configuración de BFS.
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/config")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await http.PutAsJsonAsync("/api/config", new Dictionary<string, string> { ["aduana"] = "true" })).StatusCode);
    }

    [Fact]
    public async Task Login_devuelve_los_permisos_del_rol()
    {
        await using var api = await TestApi.Iniciar();
        using var http = api.Cliente();
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email = DbInitializer.SeedAdminEmail, password = TestApi.ClaveSeed });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        var permisos = body.GetProperty("permisos").EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains("usuarios.gestionar", permisos);
        Assert.DoesNotContain("config.bfs", permisos);
    }

    [Fact]
    public async Task Operador_no_carga_peso_a_mano_ni_edita_catalogos_ni_usuarios()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        (await CrearUsuario(admin, "op@planta.com", "operador")).EnsureSuccessStatusCode();
        using var op = await api.Login("op@planta.com", "clave123");

        var manual = await op.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AA111AA", pesoEntrada = 1, taraPallets = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, manual.StatusCode);
        Assert.Contains("peso a mano", await manual.Content.ReadAsStringAsync());

        // Sin peso explícito usa la balanza: eso sí puede.
        (await op.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AA111AA", taraPallets = 0 })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/catalogos/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Usuario_cliente_solo_ve_sus_pesadas()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var agro = await CrearCliente(admin, "Agro SA");
        await CrearCliente(admin, "Otra SRL");

        // El operador escribe la razón social a mano: se asocia al cliente del catálogo igual.
        await Entrada(admin, cliente: "agro sa");
        await Entrada(admin, cliente: "Otra SRL");
        await Entrada(admin, cliente: null);

        (await CrearUsuario(admin, "compras@agro.com", "cliente", agro)).EnsureSuccessStatusCode();
        using var cliente = await api.Login("compras@agro.com", "clave123");

        var propias = await cliente.GetFromJsonAsync<JsonElement>("/api/pesadas");
        Assert.Equal(1, propias.GetArrayLength());
        Assert.Equal(agro, propias[0].GetProperty("clienteId").GetGuid());

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "X", taraPallets = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/catalogos/clientes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/scale/peso")).StatusCode);
    }

    [Fact]
    public async Task Alta_de_usuarios_valida_email_clave_rol_y_cliente()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        Assert.Equal(HttpStatusCode.BadRequest, (await CrearUsuario(admin, "sin-arroba", "operador")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearUsuario(admin, "a@b.com", "operador", clave: "123")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearUsuario(admin, "c@agro.com", "cliente")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CrearUsuario(admin, "v@bfs.com", "vendor")).StatusCode);

        (await CrearUsuario(admin, "Repetido@Planta.com", "supervisor")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await CrearUsuario(admin, "repetido@planta.com", "operador")).StatusCode);

        var roles = await admin.GetFromJsonAsync<string[]>("/api/usuarios/roles");
        Assert.DoesNotContain("vendor", roles!);
        Assert.Contains("cliente", roles!);
    }

    [Fact]
    public async Task Sin_licencia_no_hay_entradas_nuevas()
    {
        await using var api = await TestApi.Iniciar(sinLicencia: false);
        using var admin = await api.LoginAdmin();

        var licencia = await admin.GetFromJsonAsync<JsonElement>("/api/licencia");
        Assert.Equal("Ausente", licencia.GetProperty("estado").GetString());
        Assert.False(licencia.GetProperty("puedePesar").GetBoolean());

        var entrada = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AA111AA", taraPallets = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, entrada.StatusCode);
        Assert.Contains("sin licencia", await entrada.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        // Solo BFS activa licencias, y una inválida no se guarda.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/licencia", new { texto = "x.y" })).StatusCode);
        using var vendor = await api.LoginVendor();
        Assert.Equal(HttpStatusCode.BadRequest, (await vendor.PostAsJsonAsync("/api/licencia", new { texto = "x.y" })).StatusCode);
        Assert.False(File.Exists(Path.Combine(api.DataPath, "license.key")));
    }

    [Fact]
    public void Modo_nube_sin_cadena_de_conexion_falla_al_arrancar()
    {
        using var tmp = new TempDir();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiHost.Create(["--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={tmp.Path}", "--Bfs:Modo=Nube"]));
        Assert.Contains("ConnectionStrings:Bfs", ex.Message);
    }

    private static Task<HttpResponseMessage> CrearUsuario(HttpClient http, string email, string rol, Guid? clienteId = null, string clave = "clave123") =>
        http.PostAsJsonAsync("/api/usuarios", new { email, password = clave, nombre = email, rol, clienteId });

    private static async Task<Guid> CrearCliente(HttpClient http, string razonSocial)
    {
        var res = await http.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task Entrada(HttpClient http, string? cliente)
    {
        var res = await http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AA111AA", cliente, taraPallets = 0 });
        res.EnsureSuccessStatusCode();
    }
}
