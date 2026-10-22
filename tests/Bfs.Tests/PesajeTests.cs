using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Infrastructure.Data;

namespace Bfs.Tests;

/// <summary>El pesaje de punta a punta por la API (balanza simulada, sin exigir estabilidad).</summary>
public class PesajeTests
{
    private static object Entrada(string patente = "AB123CD", int? peso = 30000, int pallets = 0, string? clave = null,
        string? tipoOperacion = null, object? aduana = null) =>
        new { empresaId = TestApi.Empresa1, patenteChasis = patente, pesoEntrada = peso, taraPallets = pallets, claveManual = clave, tipoOperacion, aduana };

    [Fact]
    public async Task Tickets_numerados_por_puesto_sin_repetirse()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        Assert.Equal("01-000001", await Ticket(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada())));
        await api.Configurar(new() { ["puesto"] = "3" });
        Assert.Equal("03-000001", await Ticket(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada())));
        Assert.Equal("03-000002", await Ticket(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada())));

        // Varias a la vez: ninguna repite número (el contador tiene control de versión y se reintenta).
        var tickets = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Ticket(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada()))));
        Assert.Equal(8, tickets.Distinct().Count());
        Assert.All(tickets, t => Assert.StartsWith("03-", t));
    }

    [Fact]
    public async Task Ingreso_manual_pide_permiso_habilitacion_y_clave()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "op@planta.com", password = "clave123", nombre = "Op", rol = "operador" })).EnsureSuccessStatusCode();
        using var op = await api.Login("op@planta.com", "clave123");

        await Prohibido(op.PostAsJsonAsync("/api/pesadas/entrada", Entrada()), "permiso");

        (await admin.PutAsJsonAsync("/api/planta/clave-manual", new { clave = "4321" })).EnsureSuccessStatusCode();
        await Prohibido(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada()), "Clave");
        await Prohibido(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(clave: "0000")), "Clave");

        var ok = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(clave: "4321")));
        Assert.Equal(DbInitializer.SeedAdminEmail, ok.GetProperty("entradaManualPor").GetString());
        Assert.Equal(JsonValueKind.Null, ok.GetProperty("balanza").ValueKind);

        await api.Configurar(new() { ["ingreso_manual"] = "false" });
        await Prohibido(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(clave: "4321")), "deshabilitado");

        // Desde la balanza no hace falta nada de eso.
        var balanza = await Json(op.PostAsJsonAsync("/api/pesadas/entrada", Entrada(peso: null)));
        Assert.Equal(JsonValueKind.Null, balanza.GetProperty("entradaManualPor").ValueKind);
        Assert.Equal("Balanza 1", balanza.GetProperty("balanza").GetString());
    }

    [Fact]
    public async Task Tara_conocida_en_un_paso_y_tomar_tara()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();

        await Prohibido(admin.PostAsJsonAsync("/api/taras/tomar", new { identificador = "AA111AA", pesoManual = 14000 }), "habilitada");
        await api.Configurar(new() { ["tara_conocida"] = "true" });

        var sinTara = await admin.PostAsJsonAsync("/api/pesadas/tara-conocida", new { datos = Entrada("AA111AA", 40000) });
        Assert.Equal(HttpStatusCode.BadRequest, sinTara.StatusCode);
        Assert.Contains("No hay tara", await sinTara.Content.ReadAsStringAsync());

        var tara = await Json(admin.PostAsJsonAsync("/api/taras/tomar", new { identificador = "aa 111 aa", descripcion = "Batea", pesoManual = 14000 }));
        Assert.Equal("AA111AA", tara.GetProperty("identificador").GetString());
        Assert.Equal(14000, tara.GetProperty("kilogramos").GetInt32());

        var pesada = await Json(admin.PostAsJsonAsync("/api/pesadas/tara-conocida", new { datos = Entrada("AA111AA", 40000, pallets: 500) }));
        Assert.Equal("SALIDA", pesada.GetProperty("estado").GetString());
        Assert.Equal("TaraConocida", pesada.GetProperty("modo").GetString());
        Assert.Equal(40000, pesada.GetProperty("bruto").GetInt32());
        Assert.Equal(14000, pesada.GetProperty("tara").GetInt32());
        Assert.Equal(25500, pesada.GetProperty("neto").GetInt32());

        // Usar otra tara es ingreso manual: un operador no puede.
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "op@planta.com", password = "clave123", nombre = "Op", rol = "operador" })).EnsureSuccessStatusCode();
        using var op = await api.Login("op@planta.com", "clave123");
        await Prohibido(op.PostAsJsonAsync("/api/pesadas/tara-conocida", new { datos = Entrada("AA111AA", null), taraManual = 13000 }), "permiso");

        // Volver a tomar la tara la actualiza.
        tara = await Json(admin.PostAsJsonAsync("/api/taras/tomar", new { identificador = "AA111AA", pesoManual = 14200 }));
        Assert.Equal(14200, tara.GetProperty("kilogramos").GetInt32());
        Assert.Equal("Batea", tara.GetProperty("descripcion").GetString());
    }

    [Theory]
    [InlineData("Ingresa con Contenedor", 2000, 18000, 20000, 18000)]
    [InlineData("Carga Contenedor Lleno", 2000, 18000, 18000, 16000)]
    public async Task Aduana_calcula_el_contenedor_como_la_3x(string escenario, int taraCont, int neto, int contBruto, int contNeto)
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var aduana = new { escenario, exportador = "Export SA", exportadorCuit = "20123456786", contenedorId = "msku1234567", contenedorTara = taraCont, choferNacionalidad = "Paraguaya" };

        await Prohibido(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(tipoOperacion: "Aduana", aduana: aduana)), "aduana");
        await api.Configurar(new() { ["aduana"] = "true" });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(tipoOperacion: "Aduana", aduana: new { escenario = "otro" }))).StatusCode);

        var entrada = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(peso: 12000 + neto, tipoOperacion: "Aduana", aduana: aduana)));
        Assert.Equal("Aduana", entrada.GetProperty("tipoOperacion").GetString());
        Assert.Equal("20-12345678-6", entrada.GetProperty("aduana").GetProperty("exportadorCuit").GetString());
        Assert.Equal("MSKU1234567", entrada.GetProperty("aduana").GetProperty("contenedorId").GetString());

        var id = entrada.GetProperty("id").GetGuid();
        var salida = await Json(admin.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 }));
        Assert.Equal(neto, salida.GetProperty("neto").GetInt32());
        Assert.Equal(contBruto, salida.GetProperty("aduana").GetProperty("contenedorBruto").GetInt32());
        Assert.Equal(contNeto, salida.GetProperty("aduana").GetProperty("contenedorNeto").GetInt32());

        // La nacionalidad del chofer queda para autocompletar.
        var vehiculo = await admin.GetFromJsonAsync<JsonElement>("/api/vehiculos/AB123CD");
        Assert.Equal("Paraguaya", vehiculo.GetProperty("datos").GetProperty("nacionalidad").GetString());
    }

    [Fact]
    public async Task Anular_deja_registro_y_saca_de_pendientes()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var a = (await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada("AA111AA")))).GetProperty("id").GetGuid();
        await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada("BB222BB")));
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/api/pesadas/pendientes")).GetArrayLength());

        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "op@planta.com", password = "clave123", nombre = "Op", rol = "operador" })).EnsureSuccessStatusCode();
        using var op = await api.Login("op@planta.com", "clave123");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync($"/api/pesadas/{a}/anular", new { motivo = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/pesadas/{a}/anular", new { motivo = " " })).StatusCode);

        var anulada = await Json(admin.PostAsJsonAsync($"/api/pesadas/{a}/anular", new { motivo = "Camión equivocado" }));
        Assert.Equal("ANULADA", anulada.GetProperty("estado").GetString());
        Assert.Equal("Camión equivocado", anulada.GetProperty("motivoAnulacion").GetString());
        Assert.Equal(DbInitializer.SeedAdminEmail, anulada.GetProperty("anuladaPor").GetString());

        var pendientes = await admin.GetFromJsonAsync<JsonElement>("/api/pesadas/pendientes");
        Assert.Equal(["BB222BB"], pendientes.EnumerateArray().Select(p => p.GetProperty("patenteChasis").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/pesadas/{a}/salida", new { pesoSalida = 1000 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/pesadas/{a}/anular", new { motivo = "otra vez" })).StatusCode);

        // Sigue en el listado general (no se borra) y el número no se reutiliza.
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/api/pesadas")).GetArrayLength());
        Assert.Equal("01-000003", await Ticket(admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada())));
    }

    private static async Task<JsonElement> Json(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> Ticket(Task<HttpResponseMessage> request) =>
        (await Json(request)).GetProperty("ticketNumero").GetString()!;

    private static async Task Prohibido(Task<HttpResponseMessage> request, string mensaje)
    {
        var res = await request;
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains(mensaje, await res.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
