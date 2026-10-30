using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Bfs.Api;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Sincronizacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

/// <summary>Una nube y una portería de verdad, hablando por HTTP.</summary>
public class SyncTests : IAsyncLifetime
{
    private const string SaEmail = "sa@bfs.com.ar";
    private const string SaClave = "superclave1";
    private const string AdminAcopio = "admin@acopio.com";
    private const string ClaveAdmin = "clave-admin-1";

    private readonly TempDir _datosNube = new();
    private readonly TempDir _datosNodo = new();
    private readonly NubeDePrueba _motor = new();
    private readonly ECDsa _claveLicencias = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly int _puertoNube = PuertoLibre();
    private WebApplication? _nube;
    private WebApplication _nodo = null!;
    private Guid _tenant;
    private string _codigo = "";

    private string UrlNube => $"http://127.0.0.1:{_puertoNube}";

    public async Task InitializeAsync()
    {
        await LevantarNube();
        using var sa = await Login(UrlNube, SaEmail, SaClave);
        _tenant = (await Json(sa.PostAsJsonAsync("/api/superadmin/tenants", new { nombre = "Acopio", codigo = "acopio", adminEmail = AdminAcopio, adminNombre = "Laura", adminPassword = ClaveAdmin })))
            .GetProperty("id").GetGuid();
        _codigo = (await Json(sa.PostAsJsonAsync($"/api/superadmin/tenants/{_tenant}/nodos", new { nombre = "Balanza principal" }))).GetProperty("codigo").GetString()!;

        _nodo = ApiHost.Create([
            "--Urls=http://127.0.0.1:0", $"--Bfs:DataPath={_datosNodo.Path}", "--environment=Development",
            // Como en una instalación real: sin licencia no pesa (appsettings.Development.json la apaga).
            "--Bfs:Licencia:Requerida=true",
            $"--Bfs:Licencia:ClavePublicaNube={_claveLicencias.ExportSubjectPublicKeyInfoPem()}"
        ]);
        await _nodo.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _nodo.DisposeAsync();
        if (_nube != null) await _nube.DisposeAsync();
        _claveLicencias.Dispose();
        await _motor.LimpiarAsync(_datosNube.Path);
        _datosNodo.Dispose();
        _datosNube.Dispose();
    }

    private async Task LevantarNube(int diasLicencia = 15)
    {
        _nube = ApiHost.Create([
            $"--Urls={UrlNube}", $"--Bfs:DataPath={_datosNube.Path}", "--environment=Development",
            "--Bfs:Modo=Nube", $"--Bfs:Nube:SuperadminEmail={SaEmail}", $"--Bfs:Nube:SuperadminPassword={SaClave}",
            $"--Bfs:Nube:ClaveLicencias={_claveLicencias.ExportPkcs8PrivateKeyPem()}", $"--Bfs:Nube:DiasLicencia={diasLicencia}",
            .. _motor.Args()
        ]);
        await _nube.StartAsync();
    }

    private async Task BajarNube()
    {
        await _nube!.StopAsync();
        await _nube.DisposeAsync();
        _nube = null;
    }

    private string UrlNodo => _nodo.Urls.First();

    private async Task<HttpClient> Conectar()
    {
        var bfs = await Login(UrlNodo, DbInitializer.SeedVendorEmail, "bfs-setup");
        // Como lo tipearía una persona: minúsculas y con espacios en vez de guiones.
        await Json(bfs.PostAsJsonAsync("/api/nube/activar", new { url = UrlNube, codigo = $" {_codigo.Replace('-', ' ').ToLowerInvariant()} " }));
        return bfs;
    }

    [Fact]
    public async Task Activar_subir_pesadas_bajar_usuarios_y_anulaciones()
    {
        using var bfs = await Conectar();
        var estado = await bfs.GetFromJsonAsync<JsonElement>("/api/nube");
        Assert.True(estado.GetProperty("conectado").GetBoolean());
        Assert.Equal("Acopio", estado.GetProperty("tenant").GetString());
        // Lo que acaba de bajar de la nube no figura como pendiente de envío.
        Assert.Equal(0, estado.GetProperty("pendientes").GetInt32());

        // El código es de un solo uso.
        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente(UrlNube).PostAsJsonAsync("/api/sync/activar", new { codigo = _codigo, maquina = "MAC-X" })).StatusCode);

        // La licencia la renueva la nube: 15 días.
        var licencia = await bfs.GetFromJsonAsync<JsonElement>("/api/licencia");
        Assert.Equal("Valida", licencia.GetProperty("estado").GetString());
        Assert.InRange(licencia.GetProperty("vence").GetDateTimeOffset() - DateTimeOffset.UtcNow, TimeSpan.FromDays(14.9), TimeSpan.FromDays(15.1));

        // El administrador creado en la nube puede entrar en la portería (aunque no haya internet).
        using var admin = await Login(UrlNodo, AdminAcopio, ClaveAdmin);
        var id = await Pesar(admin, "AA111AA");
        await Json(bfs.PostAsync("/api/nube/sincronizar", null));

        using var oficina = await Login(UrlNube, AdminAcopio, ClaveAdmin);
        var enNube = await oficina.GetFromJsonAsync<JsonElement>("/api/pesadas");
        Assert.Equal(1, enNube.GetArrayLength());
        Assert.Equal("AA111AA", enNube[0].GetProperty("patenteChasis").GetString());

        // La oficina anula desde la nube; la portería se entera en la próxima vuelta.
        await Json(oficina.PostAsJsonAsync($"/api/pesadas/{id}/anular", new { motivo = "Cargado dos veces" }));
        await Json(bfs.PostAsync("/api/nube/sincronizar", null));
        var local = (await admin.GetFromJsonAsync<JsonElement>("/api/pesadas")).EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal("ANULADA", local.GetProperty("estado").GetString());

        // Sincronizar de nuevo no duplica nada.
        await Json(bfs.PostAsync("/api/nube/sincronizar", null));
        Assert.Equal(1, (await oficina.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());

        // Los usuarios de prueba de la portería no suben a la nube.
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Cliente(UrlNube).PostAsJsonAsync("/api/auth/login", new { email = DbInitializer.SeedVendorEmail, password = "bfs-setup" })).StatusCode);
    }

    [Fact]
    public async Task Sin_internet_la_porteria_sigue_y_manda_todo_al_volver()
    {
        using var bfs = await Conectar();
        using var admin = await Login(UrlNodo, AdminAcopio, ClaveAdmin);
        await BajarNube();

        await Pesar(admin, "AA111AA");
        await Pesar(admin, "BB222BB");
        Assert.Equal(HttpStatusCode.BadRequest, (await bfs.PostAsync("/api/nube/sincronizar", null)).StatusCode);
        var estado = await bfs.GetFromJsonAsync<JsonElement>("/api/nube");
        Assert.Contains("Sin conexión", estado.GetProperty("ultimoError").GetString());
        Assert.True(estado.GetProperty("pendientes").GetInt32() >= 2);

        await LevantarNube();
        await Json(bfs.PostAsync("/api/nube/sincronizar", null));
        await Json(bfs.PostAsync("/api/nube/sincronizar", null));
        Assert.Equal(JsonValueKind.Null, (await bfs.GetFromJsonAsync<JsonElement>("/api/nube")).GetProperty("ultimoError").ValueKind);

        using var oficina = await Login(UrlNube, AdminAcopio, ClaveAdmin);
        var patentes = (await oficina.GetFromJsonAsync<JsonElement>("/api/pesadas")).EnumerateArray().Select(p => p.GetProperty("patenteChasis").GetString()).Order();
        Assert.Equal(["AA111AA", "BB222BB"], patentes);
    }

    [Fact]
    public async Task Suspendido_no_sincroniza_y_la_licencia_corre_hasta_vencer()
    {
        using var bfs = await Conectar();
        using var sa = await Login(UrlNube, SaEmail, SaClave);
        await Json(sa.PutAsJsonAsync($"/api/superadmin/tenants/{_tenant}/estado", new { estado = "Suspendido", motivo = "Falta de pago" }));

        var res = await bfs.PostAsync("/api/nube/sincronizar", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("suspendida", await res.Content.ReadAsStringAsync());
        // Todavía tiene los 15 días de la última licencia.
        Assert.Equal("Valida", (await bfs.GetFromJsonAsync<JsonElement>("/api/licencia")).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Con_la_licencia_vencida_no_se_pesa()
    {
        // Una nube que da licencias de 0 días: vencen enseguida.
        await BajarNube();
        await LevantarNube(diasLicencia: 0);
        using var bfs = await Conectar();
        await Task.Delay(50);

        // Una licencia que llega ya vencida ni se guarda: la portería queda sin licencia válida.
        Assert.False((await bfs.GetFromJsonAsync<JsonElement>("/api/licencia")).GetProperty("puedePesar").GetBoolean());
        using var admin = await Login(UrlNodo, AdminAcopio, ClaveAdmin);
        var empresa = (await admin.GetFromJsonAsync<JsonElement>("/api/empresas"))[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = empresa, patenteChasis = "AA111AA", pesoEntrada = 30000, taraPallets = 0 })).StatusCode);
    }

    [Fact]
    public async Task Un_codigo_nuevo_desconecta_la_pc_anterior()
    {
        using var bfs = await Conectar();
        using var sa = await Login(UrlNube, SaEmail, SaClave);
        var nodo = (await sa.GetFromJsonAsync<JsonElement>($"/api/superadmin/tenants/{_tenant}/nodos"))[0];
        Assert.False(string.IsNullOrEmpty(nodo.GetProperty("maquina").GetString()));
        await Json(sa.PostAsJsonAsync($"/api/superadmin/tenants/{_tenant}/nodos", new { nodoId = nodo.GetProperty("id").GetGuid() }));

        var res = await bfs.PostAsync("/api/nube/sincronizar", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("código nuevo", await res.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> Pesar(HttpClient http, string patente)
    {
        var empresa = (await http.GetFromJsonAsync<JsonElement>("/api/empresas")).EnumerateArray().First(e => e.GetProperty("nombre").GetString() == "Empresa 1").GetProperty("id").GetGuid();
        return (await Json(http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = empresa, patenteChasis = patente, pesoEntrada = 30000, taraPallets = 0 })))
            .GetProperty("id").GetGuid();
    }

    private static HttpClient Cliente(string url) => new() { BaseAddress = new Uri(url) };

    private static async Task<HttpClient> Login(string url, string email, string clave)
    {
        var http = Cliente(url);
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email, password = clave });
        Assert.True(res.IsSuccessStatusCode, $"Login {email} en {url}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
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

    private static int PuertoLibre()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var puerto = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return puerto;
    }
}

/// <summary>Reglas del aplicador, sin red.</summary>
public class AplicadorSyncTests : IAsyncLifetime
{
    private readonly TempDir _tmp = new();
    private BfsDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(_tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(NodoInfo.Ninguno));
        _db = new BfsDbContext(o.Options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _tmp.Dispose();
    }

    private static JsonElement Fila(Producto p) => JsonSerializer.SerializeToElement(p, AplicadorSync.Json);

    [Fact]
    public async Task Gana_lo_mas_nuevo_y_aplicar_dos_veces_no_cambia_nada()
    {
        var id = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var v1 = new Producto { Id = id, Nombre = "Soja", Codigo = "1", ModificadoEn = t0, Version = 1 };
        var v2 = new Producto { Id = id, Nombre = "Soja", Codigo = "2", ModificadoEn = t0.AddMinutes(5), Version = 2 };

        Assert.Equal(1, (await AplicadorSync.AplicarAsync(_db, TiposSync.Productos, [Fila(v1)], esNube: false)).Aplicados);
        Assert.Equal(1, (await AplicadorSync.AplicarAsync(_db, TiposSync.Productos, [Fila(v2)], esNube: false)).Aplicados);
        var r = await AplicadorSync.AplicarAsync(_db, TiposSync.Productos, [Fila(v1), Fila(v2)], esNube: false);
        Assert.Equal((0, 2), (r.Aplicados, r.Iguales));

        var guardado = await _db.Productos.SingleAsync();
        Assert.Equal(("2", 2L, t0.AddMinutes(5)), (guardado.Codigo, guardado.Version, guardado.ModificadoEn));   // sin re-sellar
    }

    [Fact]
    public async Task El_mismo_nombre_con_otro_id_es_conflicto()
    {
        _db.Productos.Add(new Producto { Nombre = "Soja" });
        await _db.SaveChangesAsync();
        var r = await AplicadorSync.AplicarAsync(_db, TiposSync.Productos,
            [Fila(new Producto { Id = Guid.NewGuid(), Nombre = "Soja", ModificadoEn = DateTimeOffset.UtcNow, Version = 1 })], esNube: false);
        Assert.Equal(0, r.Aplicados);
        Assert.Single(r.Conflictos);
        Assert.Equal(1, await _db.Productos.CountAsync());
    }
}
