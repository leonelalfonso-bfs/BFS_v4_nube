using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace Bfs.Tests;

public class SistemaTests
{
    [Fact]
    public async Task Reportes_filtran_totalizan_y_paginan()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Agro SA" });
        var cerrada = await Entrada(admin, "AA111AA", "Agro SA", "Soja", 30000);
        (await admin.PostAsJsonAsync($"/api/pesadas/{cerrada}/salida", new { pesoSalida = 12000 })).EnsureSuccessStatusCode();
        await Entrada(admin, "BB222BB", "Otra", "Maíz", 25000);
        var anulada = await Entrada(admin, "CC333CC", "Agro SA", "Soja", 20000);
        (await admin.PostAsJsonAsync($"/api/pesadas/{anulada}/anular", new { motivo = "error" })).EnsureSuccessStatusCode();

        var todo = await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas");
        Assert.Equal(3, todo.GetProperty("total").GetInt32());
        var t = todo.GetProperty("totales");
        Assert.Equal((1, 1, 1, 18000L), (t.GetProperty("cerradas").GetInt32(), t.GetProperty("pendientes").GetInt32(), t.GetProperty("anuladas").GetInt32(), t.GetProperty("neto").GetInt64()));

        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?cliente=agro")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?estado=salida")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?texto=bb 222")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?desde=2020-01-01&hasta=2020-12-31")).GetProperty("total").GetInt32());
        var hoy = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal(3, (await admin.GetFromJsonAsync<JsonElement>($"/api/reportes/pesadas?desde={hoy}&hasta={hoy}")).GetProperty("total").GetInt32());

        var pagina2 = await admin.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?tamano=2&pagina=2");
        Assert.Equal(1, pagina2.GetProperty("items").GetArrayLength());
        Assert.Equal(3, pagina2.GetProperty("total").GetInt32());

        var resumen = await admin.GetFromJsonAsync<JsonElement>("/api/reportes/resumen?dias=7");
        Assert.Equal(18000, resumen.GetProperty("hoy").GetProperty("neto").GetInt64());
        Assert.Equal(1, resumen.GetProperty("pendientesAhora").GetInt32());
        Assert.Equal(7, resumen.GetProperty("porDia").GetArrayLength());
        Assert.Equal("Agro SA", resumen.GetProperty("topClientes")[0].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task El_cliente_solo_ve_lo_suyo_aunque_pida_otra_cosa()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var agro = (await (await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Agro SA" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await Entrada(admin, "AA111AA", "Agro SA", "Soja", 30000);
        await Entrada(admin, "BB222BB", "Otra", "Soja", 30000);
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "c@agro.com", password = "clave123", nombre = "C", rol = "cliente", clienteId = agro })).EnsureSuccessStatusCode();
        using var cliente = await api.Login("c@agro.com", "clave123");

        // Pedir las de otro cliente no sirve: el filtro se aplica dentro de lo suyo.
        Assert.Equal(0, (await cliente.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas?cliente=otra")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await cliente.GetFromJsonAsync<JsonElement>("/api/reportes/pesadas")).GetProperty("total").GetInt32());
        Assert.Equal(1, (await cliente.GetFromJsonAsync<JsonElement>("/api/reportes/resumen")).GetProperty("pendientesAhora").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/sistema")).StatusCode);
    }

    [Fact]
    public async Task Backup_descarga_y_restauracion_validada()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        await Entrada(admin, "AA111AA", null, null, 30000);

        var backup = await (await admin.PostAsync("/api/sistema/backups", null)).Content.ReadFromJsonAsync<JsonElement>();
        var archivo = backup.GetProperty("archivo").GetString()!;
        var bytes = await admin.GetByteArrayAsync($"/api/sistema/backups/{archivo}");
        Assert.Null(ArchivosBase.Validar(Guardar(bytes)));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/sistema/backups/..%2Fbfs.db")).StatusCode);

        // Un archivo cualquiera o una base de la 3.x no se aceptan para restaurar.
        Assert.Equal(HttpStatusCode.BadRequest, (await Subir(admin, "/api/sistema/restaurar", "no soy una base"u8.ToArray())).StatusCode);
        var res = await Subir(admin, "/api/sistema/restaurar", bytes);
        res.EnsureSuccessStatusCode();
        Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/sistema")).GetProperty("restauracionPendiente").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/sistema/restaurar")).StatusCode);
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/sistema")).GetProperty("restauracionPendiente").GetBoolean());
    }

    [Fact]
    public void La_restauracion_pendiente_reemplaza_la_base_y_guarda_la_anterior()
    {
        using var tmp = new TempDir();
        var actual = tmp.File("bfs.db");
        var restaurar = tmp.File("restaurar.db");
        Base(actual, "actual");
        Base(restaurar, "backup");

        var anterior = ArchivosBase.AplicarRestauracionPendiente(restaurar, actual, tmp.File("backups"));

        Assert.False(File.Exists(restaurar));
        Assert.Equal("backup", Leer(actual));
        Assert.Equal("actual", Leer(anterior!));
        Assert.Null(ArchivosBase.AplicarRestauracionPendiente(restaurar, actual, tmp.File("backups")));
    }

    [Fact]
    public async Task Espejo_y_numeracion_de_tickets()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        await Entrada(admin, "AA111AA", null, null, 30000);
        await Entrada(admin, "BB222BB", null, null, 30000);

        using var espejo = new TempDir();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/sistema/espejo", new { carpeta = "relativa" })).StatusCode);
        (await admin.PutAsJsonAsync("/api/sistema/espejo", new { carpeta = espejo.Path })).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/sistema/espejo", null)).EnsureSuccessStatusCode();
        Assert.Null(ArchivosBase.Validar(espejo.File("bfs-espejo.db")));

        var sistema = await admin.GetFromJsonAsync<JsonElement>("/api/sistema");
        var empresa1 = sistema.GetProperty("tickets").EnumerateArray().Single(t => t.GetProperty("empresaId").GetGuid() == TestApi.Empresa1);
        Assert.Equal((2, 3), (empresa1.GetProperty("maximoUsado").GetInt32(), empresa1.GetProperty("proximo").GetInt32()));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/sistema/tickets", new { empresaId = TestApi.Empresa1, proximo = 2 })).StatusCode);
        (await admin.PutAsJsonAsync("/api/sistema/tickets", new { empresaId = TestApi.Empresa1, proximo = 1000 })).EnsureSuccessStatusCode();
        var nueva = await (await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "CC333CC", pesoEntrada = 1, taraPallets = 0 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("01-001000", nueva.GetProperty("ticketNumero").GetString());
    }

    [Fact]
    public async Task Cada_instalacion_firma_sus_sesiones_con_su_propia_clave()
    {
        await using var a = await TestApi.Iniciar();
        await using var b = await TestApi.Iniciar();
        using var deA = await a.LoginAdmin();
        Assert.True(File.Exists(Path.Combine(a.DataPath, "jwt.key")));

        using var enB = b.Cliente();
        enB.DefaultRequestHeaders.Authorization = deA.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.Unauthorized, (await enB.GetAsync("/api/pesadas")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await deA.GetAsync("/api/no-existe")).StatusCode);
    }

    private static async Task<Guid> Entrada(HttpClient http, string patente, string? cliente, string? producto, int peso)
    {
        var res = await http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = patente, cliente, producto, pesoEntrada = peso, taraPallets = 0 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Subir(HttpClient http, string url, byte[] contenido)
    {
        var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(archivo, "file", "backup.db");
        return http.PostAsync(url, form);
    }

    private static string Guardar(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bfs-backup-{Guid.NewGuid():N}.db");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static void Base(string archivo, string valor)
    {
        using var c = new SqliteConnection($"Data Source={archivo};Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"CREATE TABLE t (v TEXT); INSERT INTO t VALUES ('{valor}');";
        cmd.ExecuteNonQuery();
    }

    private static string Leer(string archivo)
    {
        using var c = new SqliteConnection($"Data Source={archivo};Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT v FROM t";
        return (string)cmd.ExecuteScalar()!;
    }
}
