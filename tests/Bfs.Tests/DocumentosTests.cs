using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Documents;
using ClosedXML.Excel;

namespace Bfs.Tests;

public class DocumentosTests
{
    private static readonly DateTime Entrada = new(2026, 8, 13, 10, 31, 15);

    private static Pesada Cerrada(string modo = ModosPesaje.Normal) => new()
    {
        Id = Guid.NewGuid(), TicketNumero = "01-000001", Estado = EstadosPesada.Salida, Modo = modo, Tipo = "INGRESO",
        PatenteChasis = "ASD123", PatenteAcoplado = "QWE123", Conductor = "Alfonso Leonel", Documento = "3218752",
        Cliente = "Cliente Prueba", Transporte = "Propio", Producto = "Asfalto", Procedencia = "Mendoza", Destino = "Luján de Cuyo",
        PesoEntrada = 17020, PesoSalida = 51200, Bruto = 51200, Tara = 17020, Neto = 34180, Operador = "op@planta.com"
    };

    private static TicketDatos Datos(Pesada p) => new(
        p,
        new EmpresaTicket("Kraft Constructora SRL", "30-71061741-0", "San Francisco del Monte 2401", "General Gutierrez", "Mendoza", null, "kraft@kraftsrl.com"),
        "Comprobante de pesaje", "Gracias", null, Entrada, Entrada.AddSeconds(30), "30-11122233-4", "11-22233444-5", "0001", null);

    [Fact]
    public void Pdf_normal_tara_conocida_aduana_y_anulada()
    {
        AssertPdf(TicketPdf.Generar(Datos(Cerrada())));
        AssertPdf(TicketPdf.Generar(Datos(Cerrada(ModosPesaje.TaraConocida))));

        var anulada = Cerrada();
        anulada.Estado = EstadosPesada.Anulada;
        anulada.AnuladaPor = "sup@planta.com";
        anulada.MotivoAnulacion = "Error";
        AssertPdf(TicketPdf.Generar(Datos(anulada)));

        var aduana = Cerrada();
        aduana.TipoOperacion = TiposOperacion.Aduana;
        aduana.Aduana = new DatosAduana { Escenario = EscenariosAduana.IngresaConContenedor, ContenedorId = "MSKU1", ContenedorTara = 2000, ContenedorNeto = 34180, ContenedorBruto = 36180 };
        AssertPdf(TicketPdf.Generar(Datos(aduana) with { Aduana = new AduanaTicket("017", "LOT", "C-1", "31/03/2027") }));
    }

    [Fact]
    public void Pdf_con_logo_del_cliente()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        AssertPdf(TicketPdf.Generar(Datos(Cerrada()) with { Logo = png }));
    }

    [Theory]
    [InlineData("58", 32)]
    [InlineData("80", 48)]
    [InlineData(null, 48)]
    public void Termico_respeta_el_ancho_y_no_lleva_acentos(string? ancho, int columnas)
    {
        var bytes = TicketTermico.Generar(Datos(Cerrada()), ancho);
        Assert.All(bytes, b => Assert.True(b < 0x80, $"Byte no ASCII: {b:X2}"));

        // Sin los comandos ESC/POS, cada línea entra en el papel.
        var texto = Encoding.ASCII.GetString(bytes);
        var lineas = texto.Split('\n').Select(l => new string(l.Where(c => c >= ' ').ToArray())).ToList();
        Assert.Contains(lineas, l => l.Contains("01-000001"));
        Assert.Contains(lineas, l => l.Contains("NETO") && l.EndsWith("34180 kg"));
        Assert.Contains(lineas, l => l.Contains("Lujan de Cuyo") || l.Contains("Alfonso Leonel"));
        Assert.All(lineas.Where(l => !l.Contains("01-000001")), l => Assert.True(l.Length <= columnas + 3, $"Línea larga: '{l}'"));

        // Termina con el corte de papel.
        Assert.Equal(new byte[] { 0x1D, (byte)'V', 0x42, 0x00 }, bytes[^4..]);
    }

    [Fact]
    public void Ascii_saca_acentos_y_enies() => Assert.Equal("Perez Nandu Lujan", TicketTermico.Ascii("Pérez Ñandú Luján"));

    [Fact]
    public void Excel_con_columnas_totales_y_anuladas_sin_neto()
    {
        var anulada = Cerrada();
        anulada.TicketNumero = "01-000002";
        anulada.Estado = EstadosPesada.Anulada;
        var filas = new List<FilaExcel> { new(Cerrada(), "Kraft", Entrada, Entrada.AddMinutes(5)), new(anulada, "Kraft", Entrada, null) };

        using var wb = new XLWorkbook(new MemoryStream(PesadasExcel.Generar(filas, "Pesadas de prueba")));
        var ws = wb.Worksheet("Pesadas");
        Assert.Equal("Pesadas de prueba", ws.Cell(1, 1).GetString());
        Assert.Equal("Ticket", ws.Cell(3, 1).GetString());
        var colNeto = ws.Row(3).CellsUsed().First(c => c.GetString() == "Neto (kg)").Address.ColumnNumber;

        Assert.Equal("01-000001", ws.Cell(4, 1).GetString());
        Assert.Equal(34180, ws.Cell(4, colNeto).GetDouble());
        Assert.Equal(Entrada, ws.Cell(4, 6).GetDateTime());
        Assert.Equal("Anulada", ws.Cell(5, 2).GetString());
        Assert.True(ws.Cell(5, colNeto).IsEmpty());
        Assert.StartsWith("SUM(", ws.Cell(6, colNeto).FormulaA1);
    }

    [Fact]
    public async Task Api_ticket_termica_y_excel()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var agro = (await (await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Agro SA", cuit = "20123456786" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "Otra SRL" });
        var propia = await Entrada_(admin, "Agro SA");
        var ajena = await Entrada_(admin, "Otra SRL");

        var pdf = await admin.GetAsync($"/api/pesadas/{propia}/ticket");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        AssertPdf(await pdf.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/pesadas/{Guid.NewGuid()}/ticket")).StatusCode);

        // El usuario cliente ve su ticket; el de otro cliente "no existe".
        (await admin.PostAsJsonAsync("/api/usuarios", new { email = "c@agro.com", password = "clave123", nombre = "C", rol = "cliente", clienteId = agro })).EnsureSuccessStatusCode();
        using var cliente = await api.Login("c@agro.com", "clave123");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/pesadas/{propia}/ticket")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/pesadas/{ajena}/ticket")).StatusCode);

        // Térmica: deshabilitada, sin impresora elegida y con una que no existe.
        Assert.Contains("habilitada", await Texto(admin.PostAsync($"/api/pesadas/{propia}/termica", null)));
        await api.Configurar(new() { ["termica"] = "true" });
        Assert.Contains("elegir la impresora", await Texto(admin.PostAsync($"/api/pesadas/{propia}/termica", null)));
        await api.Configurar(new() { ["termica_impresora"] = "Impresora que no existe 123" });
        Assert.Contains("No se encontró", await Texto(admin.PostAsync($"/api/pesadas/{propia}/termica", null)));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync($"/api/pesadas/{propia}/termica", null)).StatusCode);

        // Excel: todo para el admin, solo lo suyo para el cliente, y vacío fuera de rango.
        Assert.Equal(2, FilasExcel(await admin.GetByteArrayAsync("/api/pesadas/excel")));
        Assert.Equal(1, FilasExcel(await cliente.GetByteArrayAsync("/api/pesadas/excel")));
        Assert.Equal(0, FilasExcel(await admin.GetByteArrayAsync("/api/pesadas/excel?desde=2020-01-01&hasta=2020-01-31")));
        var hoy = DateTime.Now.ToString("yyyy-MM-dd");
        Assert.Equal(2, FilasExcel(await admin.GetByteArrayAsync($"/api/pesadas/excel?desde={hoy}&hasta={hoy}")));
    }

    private static async Task<Guid> Entrada_(HttpClient http, string cliente)
    {
        var res = await http.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AA111AA", cliente, pesoEntrada = 30000, taraPallets = 0 });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<string> Texto(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        return await res.Content.ReadAsStringAsync();
    }

    private static int FilasExcel(byte[] xlsx)
    {
        using var wb = new XLWorkbook(new MemoryStream(xlsx));
        var ws = wb.Worksheet("Pesadas");
        return ws.Column(1).CellsUsed().Count(c => c.Address.RowNumber > 3 && c.GetString().Contains('-'));
    }

    private static void AssertPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 1000, "PDF demasiado chico");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
