using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Api.Pesadas;
using Bfs.Core;
using Microsoft.Data.Sqlite;

namespace Bfs.Tests;

/// <summary>Capturar el peso, completar los datos y aceptar la pesada después.</summary>
public class CapturaPesoTests
{
    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    [Fact]
    public void La_captura_vence_y_se_usa_una_vez()
    {
        var reloj = new Reloj(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
        var capturas = new CapturasPeso(reloj);
        var c = capturas.Guardar(43800, "Balanza 1");
        Assert.Equal(43800, capturas.Buscar(c.Id)!.Kilogramos);

        reloj.Ahora += CapturasPeso.Vigencia + TimeSpan.FromSeconds(1);
        Assert.Null(capturas.Buscar(c.Id));

        var otra = capturas.Guardar(15000, "Balanza 1");
        capturas.Usar(otra.Id);
        Assert.Null(capturas.Buscar(otra.Id));
    }

    [Fact]
    public async Task Entrada_y_salida_con_el_peso_capturado()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        var captura = await Capturar(admin);
        var kg = captura.GetProperty("kilogramos").GetInt32();
        Assert.True(kg > 0);

        // Faltan datos: no entra, pero la captura sigue sirviendo.
        var sinPatente = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "", capturaId = captura.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, sinPatente.StatusCode);

        var entrada = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", capturaId = captura.GetProperty("id").GetGuid() });
        Assert.True(entrada.IsSuccessStatusCode, await entrada.Content.ReadAsStringAsync());
        var p = await entrada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(kg, p.GetProperty("pesoEntrada").GetInt32());
        Assert.Equal(JsonValueKind.Null, p.GetProperty("entradaManualPor").ValueKind);

        // La misma captura no sirve para otra pesada.
        var repetida = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CE", capturaId = captura.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
        Assert.Contains("capturalo de nuevo", await repetida.Content.ReadAsStringAsync());

        var c2 = await Capturar(admin);
        var salida = await admin.PostAsJsonAsync($"/api/pesadas/{p.GetProperty("id").GetGuid()}/salida", new { capturaId = c2.GetProperty("id").GetGuid() });
        Assert.True(salida.IsSuccessStatusCode, await salida.Content.ReadAsStringAsync());
        Assert.Equal(c2.GetProperty("kilogramos").GetInt32(), (await salida.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("pesoSalida").GetInt32());
    }

    [Fact]
    public async Task El_tipo_queda_oculto_salvo_con_worms()
    {
        await using var api = await TestApi.Iniciar();
        using var http = await api.LoginAdmin();
        var campos = await http.GetFromJsonAsync<JsonElement>("/api/campos");
        var tipo = campos.EnumerateArray().Single(c => c.GetProperty("key").GetString() == "tipo");
        Assert.False(tipo.GetProperty("visible").GetBoolean());

        // Una pesada sin tipo es INGRESO (o el tipo por defecto de WORMS).
        var r = await http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000 });
        Assert.Equal("INGRESO", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task Una_instalacion_vieja_con_el_tipo_visible_lo_oculta_una_vez()
    {
        using var tmp = new TempDir();
        var o = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Bfs.Infrastructure.Data.BfsDbContext>();
        Bfs.Infrastructure.Data.BfsDatabase.Configurar(o, ModoDespliegue.Nodo, Bfs.Infrastructure.Data.BfsDatabase.SqliteConnectionString(tmp.File("bfs.db")));
        await using (var db = new Bfs.Infrastructure.Data.BfsDbContext(o.Options))
        {
            await Bfs.Infrastructure.Data.DbInitializer.SeedAsync(db);
        }

        // Como estaba antes: tipo visible y sin la marca de que ya se ocultó.
        using (var conn = new SqliteConnection($"Data Source={tmp.File("bfs.db")};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"DELETE FROM Configuraciones WHERE Clave = '{ConfigKeys.TipoOcultoAplicado}';" +
                $"UPDATE Configuraciones SET Valor = '[{{\"key\":\"tipo\",\"label\":\"Tipo\",\"visible\":true,\"required\":true}}]' WHERE Clave = '{ConfigKeys.FieldsJson}';";
            cmd.ExecuteNonQuery();
        }

        await using (var db = new Bfs.Infrastructure.Data.BfsDbContext(o.Options))
        {
            await Bfs.Infrastructure.Data.DbInitializer.SeedAsync(db);
            var json = db.Configuraciones.Single(c => c.Clave == ConfigKeys.FieldsJson).Valor;
            Assert.False(FieldCatalog.Parse(json).Single(f => f.Key == "tipo").Visible);

            // Si después BFS lo vuelve a mostrar, no se vuelve a ocultar.
            db.Configuraciones.Single(c => c.Clave == ConfigKeys.FieldsJson).Valor = json.Replace("\"visible\":false", "\"visible\":true");
            await db.SaveChangesAsync();
        }
        await using (var db = new Bfs.Infrastructure.Data.BfsDbContext(o.Options))
        {
            await Bfs.Infrastructure.Data.DbInitializer.SeedAsync(db);
            Assert.True(FieldCatalog.Parse(db.Configuraciones.Single(c => c.Clave == ConfigKeys.FieldsJson).Valor).Single(f => f.Key == "tipo").Visible);
        }
    }

    private static async Task<JsonElement> Capturar(HttpClient http)
    {
        var r = await http.PostAsJsonAsync("/api/pesadas/capturar", new { balanzaId = (Guid?)null });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }
}
