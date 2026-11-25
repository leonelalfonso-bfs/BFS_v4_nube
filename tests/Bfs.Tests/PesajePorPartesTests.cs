using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bfs.Core;

namespace Bfs.Tests;

/// <summary>Balanza corta (pesar en partes) y balanza de ejes (cada eje contra los límites de Vialidad).</summary>
public class PesajePorPartesTests
{
    private static async Task<JsonElement> Json(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> Capturar(HttpClient http) =>
        await Json(http.PostAsJsonAsync("/api/pesadas/capturar", new { parcial = true }));

    private static JsonElement Partes(JsonElement pesada) =>
        JsonDocument.Parse(pesada.GetProperty("partesJson").GetString()!).RootElement;

    [Fact]
    public async Task En_partes_suma_las_capturas_y_pide_al_menos_dos()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var chasis = await Capturar(admin);
        var acoplado = await Capturar(admin);
        object Pedido(params JsonElement[] cs) => new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", taraPallets = 0, modoCaptura = "partes",
            partes = cs.Select((c, i) => new { capturaId = c.GetProperty("id").GetGuid(), etiqueta = i == 0 ? "Chasis" : "Acoplado" }).ToArray()
        };

        // Apagado en la configuración: no se puede.
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/pesadas/entrada", Pedido(chasis, acoplado))).StatusCode);

        await api.Configurar(new() { [ConfigKeys.PesajePartes] = "true" });
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/pesadas/entrada", Pedido(chasis))).StatusCode);

        var p = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Pedido(chasis, acoplado)));
        var suma = chasis.GetProperty("kilogramos").GetInt32() + acoplado.GetProperty("kilogramos").GetInt32();
        Assert.Equal(suma, p.GetProperty("pesoEntrada").GetInt32());
        var entrada = Partes(p).GetProperty("entrada");
        Assert.Equal("partes", entrada.GetProperty("modo").GetString());
        Assert.Equal(["Chasis", "Acoplado"], entrada.GetProperty("partes").EnumerateArray().Select(x => x.GetProperty("etiqueta").GetString()));

        // Cada captura se usa una sola vez.
        var otra = await admin.PostAsJsonAsync("/api/pesadas/entrada", Pedido(chasis, acoplado));
        Assert.Contains("venció o ya se usó", await otra.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Por_ejes_arma_los_grupos_y_marca_el_exceso()
    {
        await using var api = await TestApi.Iniciar();
        await api.Configurar(new()
        {
            [ConfigKeys.PesajeEjes] = "true", [ConfigKeys.EjesExceso] = "ticket", [ConfigKeys.EjesLimiteTotal] = "60000",
            [ConfigKeys.EjesLimites] = JsonSerializer.Serialize(new Dictionary<string, int> { ["tandem_doble"] = 17000 })
        });
        using var admin = await api.LoginAdmin();

        // Simple delantero 6.500 (límite 6.000) y tándem doble 9.000 + 9.200 (límite configurado 17.000). Pesos a mano para fijarlos.
        object Eje(int kg, string tipo, int grupo) => new { kilogramos = kg, tipoEje = tipo, grupo };
        var pedido = new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", taraPallets = 0, modoCaptura = "ejes",
            partes = new[] { Eje(6500, "simple_simple", 0), Eje(9000, "tandem_doble", 1), Eje(9200, "tandem_doble", 1) }
        };
        var p = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", pedido));
        Assert.Equal(24700, p.GetProperty("pesoEntrada").GetInt32());
        Assert.Equal("ENTRADA", p.GetProperty("estado").GetString()); // dos pasadas: queda pendiente

        var grupos = Partes(p).GetProperty("entrada").GetProperty("grupos").EnumerateArray().ToList();
        Assert.Equal(500, grupos[0].GetProperty("exceso").GetInt32());
        Assert.Equal(18200, grupos[1].GetProperty("kilogramos").GetInt32());
        Assert.Equal(17000, grupos[1].GetProperty("limite").GetInt32());
        Assert.Equal(1200, grupos[1].GetProperty("exceso").GetInt32());

        // Un tándem doble con un solo eje no cierra.
        var mal = await admin.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", taraPallets = 0, modoCaptura = "ejes",
            partes = new[] { Eje(6000, "simple_simple", 0), Eje(9000, "tandem_doble", 1) }
        });
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);
        Assert.Contains("lleva 2 ejes", await mal.Content.ReadAsStringAsync());

        // El ticket térmico marca el exceso (la planta eligió marcarlo).
        var pesada = new Bfs.Core.Entities.Pesada
        {
            TicketNumero = p.GetProperty("ticketNumero").GetString()!, PatenteChasis = "AB123CD", PesoEntrada = 24700,
            PartesJson = p.GetProperty("partesJson").GetString()!
        };
        Bfs.Documents.TicketDatos Datos(bool marcar) => new(pesada, new("Planta", null, null, null, null, null, null), "Ticket", null, null,
            DateTime.Now, null, null, null, null, null) { MarcarExcesoEjes = marcar };
        var termico = Encoding.Latin1.GetString(Bfs.Documents.TicketTermico.Generar(Datos(true), "80"));
        Assert.Contains("EXCEDE 500 kg", termico);
        Assert.Contains("EXCEDE 1200 kg", termico);
        // Si la planta eligió solo avisar en pantalla, el ticket muestra los pesos sin marcar el exceso.
        Assert.DoesNotContain("EXCEDE", Encoding.Latin1.GetString(Bfs.Documents.TicketTermico.Generar(Datos(false), "80")));
        Assert.True(Bfs.Documents.TicketPdf.Generar(Datos(true)).Length > 1000);
    }

    [Fact]
    public async Task Por_ejes_en_una_sola_pasada_cierra_el_ticket_con_el_total()
    {
        await using var api = await TestApi.Iniciar();
        await api.Configurar(new() { [ConfigKeys.PesajeEjes] = "true", [ConfigKeys.EjesPasadas] = "una" });
        using var admin = await api.LoginAdmin();

        var ejes = new List<object>();
        for (var i = 0; i < 2; i++)
        {
            var c = await Capturar(admin);
            ejes.Add(new { capturaId = c.GetProperty("id").GetGuid(), tipoEje = i == 0 ? "simple_simple" : "simple_doble", grupo = i });
        }
        var p = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", taraPallets = 0, modoCaptura = "ejes", partes = ejes
        }));
        Assert.Equal("SALIDA", p.GetProperty("estado").GetString());
        Assert.Equal(ModosPesaje.ControlEjes, p.GetProperty("modo").GetString());
        Assert.Equal(p.GetProperty("pesoEntrada").GetInt32(), p.GetProperty("neto").GetInt32());

        using var excel = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await admin.GetByteArrayAsync("/api/pesadas/excel")));
        var ws = excel.Worksheet("Pesadas");
        var col = ws.Row(3).CellsUsed().First(c => c.GetString() == "Pesaje entrada").Address.ColumnNumber;
        Assert.Contains("Eje simple, rueda simple", ws.Cell(4, col).GetString());
    }

    [Fact]
    public void Limites_configurados_reemplazan_solo_los_que_vienen()
    {
        var l = TiposEje.Limites("{\"tandem_triple\":26000,\"otro\":1}");
        Assert.Equal(26000, l["tandem_triple"]);
        Assert.Equal(6000, l["simple_simple"]);
        Assert.False(l.ContainsKey("otro"));
        Assert.Equal(6000, TiposEje.Limites("no es json")["simple_simple"]);
    }
}
