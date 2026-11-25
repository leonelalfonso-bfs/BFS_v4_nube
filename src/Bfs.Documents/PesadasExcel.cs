using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using ClosedXML.Excel;

namespace Bfs.Documents;

/// <summary>Una fila del Excel, con nombres y fechas ya resueltos.</summary>
public record FilaExcel(Pesada Pesada, string Empresa, DateTime EntradaLocal, DateTime? SalidaLocal);

/// <summary>
/// Exportación de pesadas a .xlsx (equivalente a <c>ExcelExporter.java</c>), con totales y filtros de Excel.
/// Lleva todos los datos que se cargan al abrir el ticket; las correcciones van en una hoja aparte.
/// </summary>
public static class PesadasExcel
{
    /// <summary>Edición Inagro: van después de "Remito", solo si alguna pesada del Excel tiene esos datos.</summary>
    private static readonly (string Nombre, Func<FilaExcel, XLCellValue> Valor)[] ColumnasInagro =
    [
        ("Id de operación", f => T(MovimientosInagro.Ticket(f.Pesada.Inagro?.Movimiento))),
        ("Peso de origen (kg)", f => Num(f.Pesada.Inagro?.PesoOrigen)),
        ("Tanque", f => T(f.Pesada.Inagro?.Tanque)),
        ("Análisis", f => T(f.Pesada.Inagro?.Analisis)),
        ("Precintos", f => T(f.Pesada.Inagro?.Precintos)),
        ("Código producto", f => T(f.Pesada.Inagro?.ProductoCodigo)),
        ("CUIT cliente", f => T(f.Pesada.Inagro?.ClienteCuit)),
        ("CUIT transporte", f => T(f.Pesada.Inagro?.TransporteCuit)),
        ("CUIL chofer", f => T(f.Pesada.Inagro?.CuilChofer)),
        ("Observaciones remito", f => T(f.Pesada.Inagro?.ObservacionesRemito)),
        ("Planta", f => T(f.Pesada.Inagro?.Planta))
    ];

    private static (string Nombre, Func<FilaExcel, XLCellValue> Valor)[] ColumnasDe(IReadOnlyList<FilaExcel> filas)
    {
        if (!filas.Any(f => f.Pesada.Inagro != null)) return Columnas;
        var despues = Array.FindIndex(Columnas, c => c.Nombre == "Remito") + 1;
        return [.. Columnas[..despues], .. ColumnasInagro, .. Columnas[despues..]];
    }

    private static readonly (string Nombre, Func<FilaExcel, XLCellValue> Valor)[] Columnas =
    [
        ("Ticket", f => f.Pesada.TicketNumero),
        ("Empresa", f => f.Empresa),
        ("Estado", f => f.Pesada.Estado switch { EstadosPesada.Entrada => "Pendiente", EstadosPesada.Salida => "Cerrada", _ => "Anulada" }),
        ("Tipo", f => f.Pesada.Tipo),
        ("Modo", f => f.Pesada.Modo switch { ModosPesaje.TaraConocida => "Tara conocida", ModosPesaje.ControlEjes => "Control por ejes", _ => "Normal" }),
        ("Operación", f => f.Pesada.TipoOperacion),
        ("Entrada", f => f.EntradaLocal),
        ("Salida", f => f.SalidaLocal.HasValue ? f.SalidaLocal.Value : Blank.Value),
        ("Patente", f => f.Pesada.PatenteChasis),
        ("Acoplado", f => T(f.Pesada.PatenteAcoplado)),
        ("Chofer", f => T(f.Pesada.Conductor)),
        ("Tipo doc.", f => T(f.Pesada.TipoDocumento)),
        ("Documento", f => T(f.Pesada.Documento)),
        ("Nacionalidad", f => T(f.Pesada.Nacionalidad)),
        ("Cliente", f => T(f.Pesada.Cliente)),
        ("Transporte", f => T(f.Pesada.Transporte)),
        ("Producto", f => T(f.Pesada.Producto)),
        ("Procedencia", f => T(f.Pesada.Procedencia)),
        ("Destino", f => T(f.Pesada.Destino)),
        ("Remito", f => T(f.Pesada.Comprobante)),
        ("Peso entrada (kg)", f => f.Pesada.PesoEntrada),
        ("Peso salida (kg)", f => Num(f.Pesada.PesoSalida)),
        ("Bruto (kg)", f => Num(f.Pesada.Bruto)),
        ("Tara (kg)", f => Num(f.Pesada.Tara)),
        ("Tara pallets (kg)", f => f.Pesada.TaraPallets),
        ("Neto (kg)", f => f.Pesada.Estado == EstadosPesada.Anulada ? Blank.Value : Num(f.Pesada.Neto)),
        ("Escenario aduana", f => T(f.Pesada.Aduana?.Escenario)),
        ("Exportador / importador", f => T(f.Pesada.Aduana?.Exportador)),
        ("CUIT exportador", f => T(f.Pesada.Aduana?.ExportadorCuit)),
        ("ATA", f => T(f.Pesada.Aduana?.Ata)),
        ("CUIT ATA", f => T(f.Pesada.Aduana?.AtaCuit)),
        ("N° de operación", f => T(f.Pesada.Aduana?.OperacionId)),
        ("Destinación aduanera", f => T(f.Pesada.Aduana?.DestinacionAduanera)),
        ("LOT", f => T(f.Pesada.Aduana?.Lot)),
        ("Contenedor", f => T(f.Pesada.Aduana?.ContenedorId)),
        ("Tara contenedor (kg)", f => f.Pesada.Aduana is { ContenedorId: not null } a ? a.ContenedorTara : Blank.Value),
        ("Bruto contenedor (kg)", f => Num(f.Pesada.Aduana?.ContenedorBruto)),
        ("Neto contenedor (kg)", f => Num(f.Pesada.Aduana?.ContenedorNeto)),
        ("Bulto", f => T(f.Pesada.Aduana?.BultoId)),
        ("Bulto bruto (kg)", f => Num(f.Pesada.Aduana?.BultoBruto)),
        ("Bulto tara (kg)", f => Num(f.Pesada.Aduana?.BultoTara)),
        ("Pesaje entrada", f => DetallePasada(PartesPesada.Leer(f.Pesada.PartesJson).Entrada)),
        ("Pesaje salida", f => DetallePasada(PartesPesada.Leer(f.Pesada.PartesJson).Salida)),
        ("Exceso por ejes (kg)", f => ExcesoEjes(PartesPesada.Leer(f.Pesada.PartesJson)) is var e and > 0 ? e : Blank.Value),
        ("Balanza entrada", f => T(f.Pesada.Balanza)),
        ("Balanza salida", f => T(f.Pesada.BalanzaSalida)),
        ("Operador", f => f.Pesada.Operador),
        ("Operador salida", f => T(f.Pesada.OperadorSalida)),
        ("Peso entrada manual por", f => T(f.Pesada.EntradaManualPor)),
        ("Peso salida manual por", f => T(f.Pesada.SalidaManualPor)),
        ("Observaciones", f => T(f.Pesada.Observaciones)),
        ("Anulada por", f => T(f.Pesada.AnuladaPor)),
        ("Motivo anulación", f => T(f.Pesada.MotivoAnulacion)),
        ("Correcciones", f => Correcciones(f.Pesada).Count is var n and > 0 ? n : Blank.Value)
    ];

    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    /// <param name="horaLocal">Para las fechas de las correcciones (las de entrada y salida ya vienen en la fila).</param>
    public static byte[] Generar(IReadOnlyList<FilaExcel> filas, string titulo, Func<DateTimeOffset, DateTime>? horaLocal = null)
    {
        horaLocal ??= d => d.LocalDateTime;
        var columnas = ColumnasDe(filas);
        int Col(string nombre) => Array.FindIndex(columnas, c => c.Nombre == nombre) + 1;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Pesadas");

        ws.Cell(1, 1).Value = titulo;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;

        const int encabezado = 3;
        for (var i = 0; i < columnas.Length; i++) ws.Cell(encabezado, i + 1).Value = columnas[i].Nombre;
        var fila = encabezado;
        foreach (var f in filas)
        {
            fila++;
            for (var i = 0; i < columnas.Length; i++) ws.Cell(fila, i + 1).Value = columnas[i].Valor(f);
        }

        var colNeto = Col("Neto (kg)");
        if (filas.Count > 0)
        {
            var total = fila + 1;
            ws.Cell(total, colNeto - 1).Value = "Total neto";
            ws.Cell(total, colNeto - 1).Style.Font.Bold = true;
            ws.Cell(total, colNeto).FormulaA1 = $"SUM({ws.Cell(encabezado + 1, colNeto).Address}:{ws.Cell(fila, colNeto).Address})";
            ws.Cell(total, colNeto).Style.Font.Bold = true;
            ws.Range(encabezado, 1, fila, columnas.Length).SetAutoFilter();
        }

        Encabezado(ws.Range(encabezado, 1, encabezado, columnas.Length));
        ws.Column(Col("Entrada")).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Column(Col("Salida")).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        foreach (var (nombre, _) in columnas.Where(c => c.Nombre.EndsWith("(kg)")))
        {
            ws.Column(Col(nombre)).Style.NumberFormat.Format = "#,##0";
        }
        ws.SheetView.FreezeRows(encabezado);
        ws.Columns().AdjustToContents(encabezado, Math.Min(fila, encabezado + 200));

        HojaCorrecciones(wb, filas, horaLocal);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Una fila por campo corregido: ticket, cuándo, quién, por qué, valor anterior y nuevo.</summary>
    private static void HojaCorrecciones(XLWorkbook wb, IReadOnlyList<FilaExcel> filas, Func<DateTimeOffset, DateTime> horaLocal)
    {
        var detalle = filas.SelectMany(f => Correcciones(f.Pesada).SelectMany(c => c.Cambios.Select(x => (f, c, x)))).ToList();
        if (detalle.Count == 0) return;

        var ws = wb.Worksheets.Add("Correcciones");
        string[] columnas = ["Ticket", "Empresa", "Fecha", "Usuario", "Motivo", "Campo", "Antes", "Después"];
        for (var i = 0; i < columnas.Length; i++) ws.Cell(1, i + 1).Value = columnas[i];
        var fila = 1;
        foreach (var (f, c, x) in detalle)
        {
            fila++;
            XLCellValue[] v = [f.Pesada.TicketNumero, f.Empresa, horaLocal(c.En), c.Por, c.Motivo, x.Campo, T(x.Antes), T(x.Despues)];
            for (var i = 0; i < v.Length; i++) ws.Cell(fila, i + 1).Value = v[i];
        }
        Encabezado(ws.Range(1, 1, 1, columnas.Length));
        ws.Column(3).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Range(1, 1, fila, columnas.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    public static List<CorreccionPesada> Correcciones(Pesada p)
    {
        try { return JsonSerializer.Deserialize<List<CorreccionPesada>>(p.CorreccionesJson, JsonWeb) ?? []; }
        catch (JsonException) { return []; }
    }

    private static void Encabezado(IXLRange header)
    {
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f766e");
        header.Style.Font.FontColor = XLColor.White;
    }

    /// <summary>"En 2 partes" o, por ejes, cada grupo: "Tándem triple, rueda doble 8500+8600+8700=25800 (lím. 25500)".</summary>
    private static XLCellValue DetallePasada(PesoPorPartes? d) => d switch
    {
        null => "",
        { Modo: ModosCaptura.Partes } => $"En {d.Partes.Count} partes",
        _ => string.Join(" · ", (d.Grupos ?? []).Select(g => $"{g.Nombre} {string.Join("+", g.Ejes)}={g.Kilogramos} (lím. {g.Limite})"))
    };

    /// <summary>El mayor exceso de la pesada: la suma de los grupos excedidos o el del total, el que sea mayor, por pasada.</summary>
    private static int ExcesoEjes(PartesPesada p) =>
        new[] { p.Entrada, p.Salida }.Where(d => d != null)
            .Select(d => Math.Max(d!.ExcesoTotal, (d.Grupos ?? []).Sum(g => g.Exceso))).DefaultIfEmpty(0).Max();

    private static XLCellValue T(string? s) => s ?? "";

    private static XLCellValue Num(int? v) => v.HasValue ? v.Value : Blank.Value;
}
