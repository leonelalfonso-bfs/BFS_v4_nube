using Bfs.Core.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Bfs.Documents.Formato;

namespace Bfs.Documents;

/// <summary>
/// Edición Inagro: los datos de la pesada para imprimir sobre el remito preimpreso A4 de INAGRO.
/// Solo texto, sin bordes ni encabezado: el formulario ya los tiene. Las posiciones son las de la 3.x
/// (<c>TicketGenerator.generateRemitoPreimpresoPdfAsBytes</c>, en puntos desde abajo a la izquierda, a la altura
/// de la línea base del texto). <paramref name="ajusteXmm"/> / <paramref name="ajusteYmm"/> corren todo junto si
/// una impresora tira el texto corrido (positivo = a la derecha / hacia abajo).
/// </summary>
public static class RemitoPreimpreso
{
    private const string Fuente = "Lato";
    private const float AltoA4 = 842f;
    private const float Tamano = 10f;
    private const float PuntosPorMm = 72f / 25.4f;

    static RemitoPreimpreso()
    {
        // Igual que TicketPdf: licencia Community (gratis para empresas que facturan menos de 1 millón de dólares por año).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generar(TicketDatos t, float ajusteXmm = 0, float ajusteYmm = 0)
    {
        var p = t.Pesada;
        var i = p.Inagro ?? new DatosInagro();
        var dx = ajusteXmm * PuntosPorMm;
        var dy = ajusteYmm * PuntosPorMm;

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontSize(Tamano).FontFamily(Fuente).LineHeight(1));
            page.Content().Layers(l =>
            {
                l.PrimaryLayer().Height(AltoA4 - 1);

                // Dónde va cada dato, con las coordenadas de la 3.x (x, línea base desde abajo).
                void En(float x, float y, string? texto)
                {
                    if (string.IsNullOrWhiteSpace(texto)) return;
                    l.Layer().TranslateX(x + dx).TranslateY(Arriba(y) + dy).Text(texto);
                }
                void Derecha(float x, float y, string texto) =>
                    l.Layer().TranslateX(x - 150 + dx).TranslateY(Arriba(y) + dy).Width(150).AlignRight().Text(texto);

                En(443, 737, Fecha(t.EntradaLocal));

                // Destinatario (cliente)
                En(140, 621, p.Cliente);
                En(140, 604, i.ClienteDireccion);
                En(140, 587, i.ClienteLocalidad);
                En(140, 570, i.ClienteCondicionIva);
                En(403, 567, i.ClienteCuit ?? t.ClienteCuit);

                // Producto y, 3 cm a la derecha, el número de ticket
                l.Layer().TranslateX(200 + dx).TranslateY(Arriba(518) + dy).Row(r =>
                {
                    r.AutoItem().Text(Texto(p.Producto));
                    r.ConstantItem(85);
                    r.AutoItem().Text($"Ticket: {p.TicketNumero}");
                });

                // Pesos alineados a la derecha de los campos del formulario
                Derecha(263, 502, Numero(p.Bruto));
                Derecha(263, 485, Numero(p.Tara));
                Derecha(263, 468, Numero(p.Neto));

                // Lugar de entrega
                En(200, 445, p.Destino);

                // Transportista
                En(140, 399, p.Transporte);
                En(140, 370, i.TransporteDireccion);
                En(140, 351, i.TransporteLocalidad);
                En(140, 331, i.TransporteCondicionIva);
                En(418, 328, i.TransporteCuit ?? t.TransporteCuit);
                En(140, 314, p.PatenteChasis);
                En(304, 311, p.PatenteAcoplado);
                En(140, 294, p.Conductor);
                En(140, 281, p.Documento);
                En(140, 213, i.Precintos);

                // Observaciones generales (con análisis y tanque), con ajuste de línea
                var general = (string.IsNullOrWhiteSpace(i.Analisis) ? "" : $"Analisis: {i.Analisis} .    ")
                    + (string.IsNullOrWhiteSpace(i.Tanque) ? "" : $"TK: {i.Tanque} .    ")
                    + (p.Observaciones ?? "");
                if (general.Trim().Length > 0)
                {
                    l.Layer().TranslateX(25 + dx).TranslateY(AltoA4 - 175 + dy).Width(540).Text(general).FontSize(11);
                }

                // Observaciones del remito: recuadro a la derecha, bajo el ticket
                if (!string.IsNullOrWhiteSpace(i.ObservacionesRemito))
                {
                    l.Layer().TranslateX(426 + dx).TranslateY(AltoA4 - 540 + dy).Width(150)
                        .Border(0.5f).Padding(3).Text(i.ObservacionesRemito).FontSize(7);
                }
            });
        })).GeneratePdf();
    }

    /// <summary>Del sistema de la 3.x (línea base desde abajo) al de QuestPDF (borde de arriba del texto desde arriba).</summary>
    private static float Arriba(float lineaBase) => AltoA4 - lineaBase - Tamano * 0.8f;

    private static string Numero(int? kg) => (kg ?? 0).ToString();
}
