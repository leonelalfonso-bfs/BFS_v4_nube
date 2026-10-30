using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api;
using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

/// <summary>Modo nube con un archivo SQLite por cliente: el mismo código que corre con PostgreSQL en el servidor.</summary>
public class NubeTests : IAsyncLifetime
{
    private const string SaEmail = "sa@bfs.com.ar";
    private const string SaClave = "superclave1";

    private readonly TempDir _tmp = new();
    private WebApplication _app = null!;

    public async Task InitializeAsync() => _app = await Levantar();

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        _tmp.Dispose();
    }

    private async Task<WebApplication> Levantar()
    {
        var app = ApiHost.Create([
            "--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={_tmp.Path}", "--environment=Development",
            "--Bfs:Modo=Nube", "--Bfs:Nube:Proveedor=Sqlite", $"--Bfs:Nube:SuperadminEmail={SaEmail}", $"--Bfs:Nube:SuperadminPassword={SaClave}"
        ]);
        await app.StartAsync();
        return app;
    }

    private HttpClient Cliente() => new() { BaseAddress = new Uri(_app.Urls.First()) };

    private async Task<HttpClient> Login(string email, string clave)
    {
        var http = Cliente();
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email, password = clave });
        Assert.True(res.IsSuccessStatusCode, $"Login {email}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static async Task<JsonElement> Json(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<HttpResponseMessage> AltaTenant(HttpClient sa, string nombre, string codigo, string email) =>
        sa.PostAsJsonAsync("/api/superadmin/tenants", new { nombre, codigo, adminEmail = email, adminNombre = "Admin", adminPassword = "clave-admin-1" });

    private static async Task<Guid> Pesar(HttpClient http, string patente)
    {
        var empresa = (await http.GetFromJsonAsync<JsonElement>("/api/empresas"))[0].GetProperty("id").GetGuid();
        return (await Json(http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = empresa, patenteChasis = patente, pesoEntrada = 30000, taraPallets = 0 })))
            .GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Alta_de_clientes_y_datos_aislados()
    {
        using var sa = await Login(SaEmail, SaClave);
        Assert.Equal(0, (await sa.GetFromJsonAsync<JsonElement>("/api/superadmin/tenants")).GetArrayLength());
        // El superadmin no es usuario de ningún cliente.
        Assert.Equal(HttpStatusCode.Forbidden, (await sa.GetAsync("/api/empresas")).StatusCode);

        await Json(AltaTenant(sa, "Acopio Sur SA", "acopio_sur", "admin@acopio.com"));
        await Json(AltaTenant(sa, "Molinos Norte", "molinos", "admin@molinos.com"));
        Assert.Equal(HttpStatusCode.BadRequest, (await AltaTenant(sa, "X", "Con Espacios", "x@x.com")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AltaTenant(sa, "Otro", "acopio_sur", "otro@x.com")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AltaTenant(sa, "Otro", "otro", "admin@acopio.com")).StatusCode);

        using var acopio = await Login("ADMIN@acopio.com", "clave-admin-1");
        using var molinos = await Login("admin@molinos.com", "clave-admin-1");
        Assert.Equal("Acopio Sur SA", (await acopio.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("tenant").GetString());

        // Cada cliente arranca con su empresa, sin los usuarios ni las empresas de prueba del nodo.
        var empresas = await acopio.GetFromJsonAsync<JsonElement>("/api/empresas");
        Assert.Equal(["Acopio Sur SA"], empresas.EnumerateArray().Select(e => e.GetProperty("nombre").GetString()));
        Assert.Equal(1, (await acopio.GetFromJsonAsync<JsonElement>("/api/usuarios")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Cliente().PostAsJsonAsync("/api/auth/login", new { email = DbInitializer.SeedAdminEmail, password = "admin" })).StatusCode);

        await Pesar(acopio, "AA111AA");
        Assert.Equal(1, (await acopio.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());
        Assert.Equal(0, (await molinos.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());

        // Un usuario de cliente no entra al panel de BFS.
        Assert.Equal(HttpStatusCode.Forbidden, (await acopio.GetAsync("/api/superadmin/tenants")).StatusCode);
    }

    [Fact]
    public async Task El_email_es_unico_entre_todos_los_clientes()
    {
        using var sa = await Login(SaEmail, SaClave);
        await Json(AltaTenant(sa, "Acopio", "acopio", "admin@acopio.com"));
        await Json(AltaTenant(sa, "Molinos", "molinos", "admin@molinos.com"));
        using var acopio = await Login("admin@acopio.com", "clave-admin-1");
        using var molinos = await Login("admin@molinos.com", "clave-admin-1");

        await Json(acopio.PostAsJsonAsync("/api/usuarios", new { email = "juan@gmail.com", password = "clave123", nombre = "Juan", rol = "operador" }));
        Assert.Equal(HttpStatusCode.Conflict, (await molinos.PostAsJsonAsync("/api/usuarios", new { email = "juan@gmail.com", password = "clave123", nombre = "Otro Juan", rol = "operador" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await molinos.PostAsJsonAsync("/api/usuarios", new { email = "admin@acopio.com", password = "clave123", nombre = "X", rol = "operador" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await molinos.PostAsJsonAsync("/api/usuarios", new { email = SaEmail, password = "clave123", nombre = "X", rol = "operador" })).StatusCode);

        // El nuevo usuario entra directo a su cliente.
        using var juan = await Login("juan@gmail.com", "clave123");
        Assert.Equal("Acopio", (await juan.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("tenant").GetString());
    }

    [Fact]
    public async Task Suspender_corta_el_acceso_y_reactivar_lo_devuelve()
    {
        using var sa = await Login(SaEmail, SaClave);
        var acopio = (await Json(AltaTenant(sa, "Acopio", "acopio", "admin@acopio.com"))).GetProperty("id").GetGuid();
        await Json(AltaTenant(sa, "Molinos", "molinos", "admin@molinos.com"));
        using var admin = await Login("admin@acopio.com", "clave-admin-1");
        using var molinos = await Login("admin@molinos.com", "clave-admin-1");

        await Json(sa.PutAsJsonAsync($"/api/superadmin/tenants/{acopio}/estado", new { estado = "Suspendido", motivo = "Falta de pago" }));

        // La sesión que ya estaba abierta deja de funcionar, y no se puede volver a entrar.
        var res = await admin.GetAsync("/api/pesadas");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains("suspendida", await res.Content.ReadAsStringAsync());
        var login = await Cliente().PostAsJsonAsync("/api/auth/login", new { email = "admin@acopio.com", password = "clave-admin-1" });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("suspendida", await login.Content.ReadAsStringAsync());

        // Los demás clientes siguen normalmente.
        Assert.Equal(HttpStatusCode.OK, (await molinos.GetAsync("/api/pesadas")).StatusCode);

        // BFS puede entrar igual (soporte), con permisos de configuración.
        var entrar = await Json(sa.PostAsync($"/api/superadmin/tenants/{acopio}/entrar", null));
        using var soporte = Cliente();
        soporte.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", entrar.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.OK, (await soporte.GetAsync("/api/config")).StatusCode);
        Assert.True((await soporte.GetFromJsonAsync<JsonElement>("/api/me")).GetProperty("soporte").GetBoolean());

        await Json(sa.PutAsJsonAsync($"/api/superadmin/tenants/{acopio}/estado", new { estado = "Activo" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/pesadas")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sa.PutAsJsonAsync($"/api/superadmin/tenants/{acopio}/estado", new { estado = "Borrado" })).StatusCode);
    }

    [Fact]
    public async Task Al_reiniciar_el_servidor_los_clientes_siguen()
    {
        using (var sa = await Login(SaEmail, SaClave))
        {
            await Json(AltaTenant(sa, "Acopio", "acopio", "admin@acopio.com"));
            using var admin = await Login("admin@acopio.com", "clave-admin-1");
            await Pesar(admin, "AA111AA");
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = await Levantar();

        using var otraVez = await Login("admin@acopio.com", "clave-admin-1");
        Assert.Equal(1, (await otraVez.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());

        // Base central y base del cliente, cada una en su archivo.
        Assert.True(File.Exists(Path.Combine(_tmp.Path, "central.db")));
        Assert.True(File.Exists(Path.Combine(_tmp.Path, "tenants", "acopio.db")));
    }

    [Fact]
    public void Migraciones_de_la_base_central_al_dia_en_los_dos_motores()
    {
        foreach (var bases in new[] { new BasesNube(ProveedorBase.Sqlite, null, _tmp.Path), new BasesNube(ProveedorBase.Postgres, "Host=localhost;Database=x", _tmp.Path) })
        {
            using var central = bases.Central();
            Assert.False(central.Database.HasPendingModelChanges(), $"Falta una migración de la base central ({bases.Proveedor})");
            Assert.NotEmpty(central.Database.GetMigrations());
        }
    }

    [Theory]
    [InlineData("acopio_sur", true)]
    [InlineData("a1", true)]
    [InlineData("A", false)]
    [InlineData("con espacio", false)]
    [InlineData("_empieza", false)]
    [InlineData("drop;table", false)]
    public void Codigo_de_cliente(string codigo, bool valido) => Assert.Equal(valido, BasesNube.CodigoValido(codigo));
}
