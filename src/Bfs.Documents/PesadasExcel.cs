using Bfs.Core;
using Bfs.Core.Entities;
using ClosedXML.Excel;

namespace Bfs.Documents;

/// <summary>Una fila del Excel, con nombres y fechas ya resueltos.</summary>
public record FilaExcel(Pesada Pesada, string Empresa, DateTime EntradaLocal, DateTime? SalidaLocal);

/// <summary>Exportación de pesadas a .xlsx (equivalente a <c>ExcelExporter.java</c>), con totales y filtros de Excel.</summary>
public static class PesadasExcel
{
    private static readonly string[] Columnas =
    [
        "Ticket", "Estado", "Modo", "Operación", "Empresa", "Entrada", "Salida", "Patente", "Acoplado", "Chofer", "Documento",
        "Cliente", "Transporte", "Producto", "Procedencia", "Destino", "Remito", "Peso entrada (kg)", "Peso salida (kg)",
        "Bruto (kg)", "Tara (kg)", "Tara pallets (kg)", "Neto (kg)", "Contenedor", "Neto contenedor (kg)",
        "Peso manual", "Operador", "Observaciones", "Motivo anulación"
    ];

    public static byte[] Generar(IReadOnlyList<FilaExcel> filas, string titulo)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Pesadas");

        ws.Cell(1, 1).Value = titulo;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;

        const int encabezado = 3;
        for (var i = 0; i < Columnas.Length; i++) ws.Cell(encabezado, i + 1).Value = Columnas[i];
        var fila = encabezado;
        foreach (var f in filas)
        {
            fila++;
            var p = f.Pesada;
            var c = 1;
            void V(XLCellValue v) => ws.Cell(fila, c++).Value = v;
            V(p.TicketNumero);
            V(p.Estado switch { EstadosPesada.Entrada => "Pendiente", EstadosPesada.Salida => "Cerrada", _ => "Anulada" });
            V(p.Modo == ModosPesaje.TaraConocida ? "Tara conocida" : "Normal");
            V(p.TipoOperacion);
            V(f.Empresa);
            V(f.EntradaLocal);
            V(f.SalidaLocal.HasValue ? f.SalidaLocal.Value : Blank.Value);
            V(p.PatenteChasis);
            V(p.PatenteAcoplado ?? "");
            V(p.Conductor ?? "");
            V(p.Documento ?? "");
            V(p.Cliente ?? "");
            V(p.Transporte ?? "");
            V(p.Producto ?? "");
            V(p.Procedencia ?? "");
            V(p.Destino ?? "");
            V(p.Comprobante ?? "");
            V(p.PesoEntrada);
            V(Num(p.PesoSalida));
            V(Num(p.Bruto));
            V(Num(p.Tara));
            V(p.TaraPallets);
            V(p.Estado == EstadosPesada.Anulada ? Blank.Value : Num(p.Neto));
            V(p.Aduana?.ContenedorId ?? "");
            V(Num(p.Aduana?.ContenedorNeto));
            V(p.EntradaManualPor != null || p.SalidaManualPor != null ? "Sí" : "");
            V(p.Operador);
            V(p.Observaciones ?? "");
            V(p.MotivoAnulacion ?? "");
        }

        var colNeto = Array.IndexOf(Columnas, "Neto (kg)") + 1;
        if (filas.Count > 0)
        {
            var total = fila + 1;
            ws.Cell(total, colNeto - 1).Value = "Total neto";
            ws.Cell(total, colNeto - 1).Style.Font.Bold = true;
            ws.Cell(total, colNeto).FormulaA1 = $"SUM({ws.Cell(encabezado + 1, colNeto).Address}:{ws.Cell(fila, colNeto).Address})";
            ws.Cell(total, colNeto).Style.Font.Bold = true;
            ws.Range(encabezado, 1, fila, Columnas.Length).SetAutoFilter();
        }

        var header = ws.Range(encabezado, 1, encabezado, Columnas.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0f766e");
        header.Style.Font.FontColor = XLColor.White;
        ws.Column(Array.IndexOf(Columnas, "Entrada") + 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        ws.Column(Array.IndexOf(Columnas, "Salida") + 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
        foreach (var nombre in Columnas.Where(n => n.EndsWith("(kg)")))
        {
            ws.Column(Array.IndexOf(Columnas, nombre) + 1).Style.NumberFormat.Format = "#,##0";
        }
        ws.SheetView.FreezeRows(encabezado);
        ws.Columns().AdjustToContents(encabezado, Math.Min(fila, encabezado + 200));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static XLCellValue Num(int? v) => v.HasValue ? v.Value : Blank.Value;
}
