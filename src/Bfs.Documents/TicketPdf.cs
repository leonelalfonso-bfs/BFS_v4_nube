using Bfs.Core;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static Bfs.Documents.Formato;

namespace Bfs.Documents;

/// <summary>
/// Ticket A4 igual al de la 3.x (<c>TicketGenerator.java</c>): el normal sale con original y duplicado en la misma hoja;
/// el de aduana, con sus secciones. Además marca el peso manual, la tara conocida y la anulación.
/// </summary>
public static class TicketPdf
{
    /// <summary>Viene incluida en QuestPDF: el ticket sale igual en la PC de portería y en el servidor Linux de la nube.</summary>
    private const string Fuente = "Lato";
    private const string Desarrollado = "Balanzas Full Service SRL - CUIT: 30-71534221-5 - Los Robles 4207, Ibarlucea, Santa Fe";

    private static readonly byte[] LogoBfs = CargarLogoBfs();

    static TicketPdf()
    {
        // QuestPDF es gratis (licencia Community) para empresas que facturan menos de 1 millón de dólares por año.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Media A4: el original arriba y el duplicado abajo, para cortar la hoja al medio y que queden iguales.</summary>
    public static readonly float MediaHoja = PageSizes.A4.Height / 2;

    private const float LineaCorte = 0.5f;

    public static byte[] Generar(TicketDatos t) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(x => x.FontSize(7.5f).FontFamily(Fuente).LineHeight(1.2f));
            if (t.EsAduana)
            {
                page.MarginHorizontal(36);
                page.MarginVertical(28);
                page.Content().Element(c => Aduana(c, t));
                return;
            }

            // Sin margen de página: cada copia es una caja de exactamente media hoja con su propio margen,
            // y la línea punteada cae justo en el medio. Si una copia no entra (observaciones muy largas) se achica.
            page.Margin(0);
            page.Content().Column(col =>
            {
                col.Item().Height(MediaHoja - LineaCorte / 2).Element(c => Copia(c, t, "TICKET ORIGINAL"));
                col.Item().LineHorizontal(LineaCorte).LineColor(Colors.Grey.Medium).LineDashPattern([4f, 4f]);
                // Con fotos de las cámaras (automatización), como la 3.x: un solo ticket y abajo las fotos.
                if (t.Fotos.Count > 0) col.Item().Height(MediaHoja - LineaCorte / 2).Element(c => Fotos(c, t));
                else col.Item().Height(MediaHoja - LineaCorte / 2).Element(c => Copia(c, t, "TICKET DUPLICADO"));
            });
        })).GeneratePdf();

    private static void Fotos(IContainer c, TicketDatos t) =>
        c.PaddingHorizontal(36).PaddingVertical(20).Column(col =>
        {
            col.Item().Text($"FOTOS · TICKET {t.Pesada.TicketNumero}").Bold();
            foreach (var momento in t.Fotos.GroupBy(f => f.Momento))
            {
                col.Item().PaddingTop(6).Text(momento.Key == "salida" ? "Salida" : "Entrada").SemiBold();
                col.Item().PaddingTop(2).Height(t.Fotos.Select(f => f.Momento).Distinct().Count() > 1 ? 150 : 300).Row(r =>
                {
                    foreach (var f in momento)
                    {
                        r.RelativeItem().PaddingRight(6).Column(x =>
                        {
                            x.Item().Height(t.Fotos.Select(y => y.Momento).Distinct().Count() > 1 ? 136 : 286).AlignCenter().Image(f.Imagen).FitArea();
                            x.Item().AlignCenter().Text(f.Camara).FontSize(6.5f);
                        });
                    }
                });
            }
        });

    private static void Copia(IContainer c, TicketDatos t, string copia) =>
        c.PaddingHorizontal(36).PaddingVertical(24).ScaleToFit().Element(x => Normal(x, t, copia));

    private static void Empresa(IContainer c, TicketDatos t)
    {
        var e = t.Empresa;
        c.Row(row =>
        {
            row.ConstantItem(210).Height(50).AlignLeft().Element(x =>
            {
                if (t.Logo is { Length: > 0 }) x.Image(t.Logo).FitArea();
            });
            row.ConstantItem(40);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(e.RazonSocial).Bold().FontSize(8.5f);
                if (e.Cuit != null) col.Item().Text($"CUIT: {e.Cuit}");
                if (e.Direccion != null) col.Item().Text($"Dirección: {e.Direccion}");
                var localidad = string.Join(" - ", new[] { e.Localidad, e.Provincia }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (localidad.Length > 0) col.Item().Text($"Localidad: {localidad}");
                if (e.Telefono != null) col.Item().Text($"Tel: {e.Telefono}");
                if (e.Email != null) col.Item().Text($"Mail: {e.Email}");
            });
        });
    }

    private static void Normal(IContainer c, TicketDatos t, string copia)
    {
        var p = t.Pesada;
        c.Column(col =>
        {
            col.Item().Element(x => Empresa(x, t));
            col.Item().PaddingVertical(10).LineHorizontal(1);

            col.Item().Row(r =>
            {
                r.RelativeItem().Text($"TICKET NUMERO: {p.TicketNumero}").Bold();
                if (t.EsAnulada) r.AutoItem().PaddingHorizontal(10).Text("ANULADA").Bold().FontColor(Colors.Red.Medium);
                else if (t.EsPendiente) r.AutoItem().PaddingHorizontal(10).Text("PENDIENTE DE SALIDA").Bold().FontColor(Colors.Orange.Darken2);
                if (t.EsTaraConocida) r.AutoItem().PaddingHorizontal(10).Text("TARA CONOCIDA").Bold();
                r.AutoItem().Text(copia).Bold();
            });
            col.Item().PaddingVertical(10).LineHorizontal(1);

            // Edición Inagro: los mismos datos en las mismas líneas que el ticket de su 3.x.
            var i = p.Inagro;
            string Extra(string etiqueta, string? valor) => string.IsNullOrWhiteSpace(valor) ? "" : $"   {etiqueta}: {valor}";
            col.Item().PaddingTop(6).Column(d =>
            {
                d.Item().Text($"Fecha Entrada: {Fecha(t.EntradaLocal)}");
                d.Item().Text($"Fecha Salida:  {Fecha(t.SalidaLocal)}");
                d.Item().Text($"Patente Chasis: {Texto(p.PatenteChasis)}   Patente Acoplado: {Texto(p.PatenteAcoplado)}");
                d.Item().Text($"Chofer: {Texto(p.Conductor)} - DNI: {Texto(p.Documento)}{Extra("CUIL", i?.CuilChofer)}");
                d.Item().Text($"Cliente: {Texto(p.Cliente)} - CUIT: {Texto(i?.ClienteCuit ?? t.ClienteCuit)}");
                d.Item().Text($"Transporte: {Texto(p.Transporte)} - CUIT: {Texto(i?.TransporteCuit ?? t.TransporteCuit)}");
                d.Item().Text($"Procedencia: {Texto(p.Procedencia)}   Destino: {Texto(p.Destino)}{Extra("Tanque", i?.Tanque)}");
                d.Item().Text($"Producto: {Texto(p.Producto)} - Cód: {Texto(i?.ProductoCodigo ?? t.ProductoCodigo)}"
                    + Extra("Análisis", i?.Analisis) + Extra("Precintos", i?.Precintos));
                var operacion = MovimientosInagro.Ticket(i?.Movimiento) is { } mov ? $"Id de Operación: {mov}   " : "";
                d.Item().Text($"{operacion}Remito: {Texto(p.Comprobante)}");
                if (i?.PesoOrigen is { } origen) d.Item().Text($"Peso de origen: {Kg(origen)}");
                d.Item().Text($"Observaciones: {Texto(p.Observaciones)}");
            });

            var (l1, v1, m1, l2, v2, m2) = t.EsTaraConocida
                ? ("Bruto:", (int?)p.PesoEntrada, p.EntradaManualPor != null, "Tara:", p.PesoSalida, p.SalidaManualPor != null)
                : ("Entrada:", (int?)p.PesoEntrada, p.EntradaManualPor != null, "Salida:", p.PesoSalida, p.SalidaManualPor != null);
            if (t.EsControlEjes)
            {
                // Control de carga por ejes en una pasada: solo el peso total.
                col.Item().PaddingTop(12).Element(x => Peso(x, "Peso total:", t.EsAnulada ? null : p.PesoEntrada, p.EntradaManualPor != null));
            }
            else
            {
                col.Item().PaddingTop(12).Row(r =>
                {
                    r.RelativeItem().Element(x => Peso(x, l1, v1, m1));
                    r.RelativeItem().Element(x => Peso(x, l2, v2, m2));
                    r.RelativeItem().Element(x => Peso(x, "Neto:", t.EsAnulada ? null : p.Neto, false));
                });
            }
            var partes = t.Partes;
            DetallePasada(col, t.EsControlEjes ? "Pesaje por ejes" : "Entrada", partes.Entrada, t.MarcarExcesoEjes);
            DetallePasada(col, "Salida", partes.Salida, t.MarcarExcesoEjes);
            if (p.TaraPallets > 0)
            {
                col.Item().PaddingTop(4).Text($"Tara pallets: {Kg(p.TaraPallets)}   |   Neto mercadería (sin pallets): {Kg(p.Neto)}");
            }
            if (p.EntradaManualPor != null || p.SalidaManualPor != null)
            {
                col.Item().PaddingTop(2).Text("* Peso cargado manualmente").Italic().FontSize(6.5f);
            }
            if (t.EsAnulada)
            {
                col.Item().PaddingTop(2).Text($"Anulada por {p.AnuladaPor}: {p.MotivoAnulacion}").FontColor(Colors.Red.Medium);
            }
            if (!string.IsNullOrWhiteSpace(t.Pie)) col.Item().PaddingTop(4).Text(t.Pie);

            col.Item().PaddingVertical(10).LineHorizontal(1);
            col.Item().Element(PieBfs);
        });
    }

    /// <summary>
    /// En partes: una línea (el detalle no importa, se pesó por partes por necesidad).
    /// Por ejes: el peso de cada grupo con su límite y, si la planta lo pidió, el exceso marcado.
    /// </summary>
    private static void DetallePasada(ColumnDescriptor col, string pasada, Bfs.Core.PesoPorPartes? d, bool marcarExceso)
    {
        if (d == null) return;
        if (d.Modo == Bfs.Core.ModosCaptura.Partes)
        {
            col.Item().PaddingTop(3).Text($"{pasada}: pesada en {d.Partes.Count} partes (balanza corta).").Italic().FontSize(6.5f);
            return;
        }
        if (d.Grupos is not { Count: > 0 } grupos) return;
        col.Item().PaddingTop(5).Text(x =>
        {
            x.Span($"{pasada} por ejes: ").Bold();
            for (var i = 0; i < grupos.Count; i++)
            {
                var g = grupos[i];
                var exceso = marcarExceso && g.Exceso > 0 ? $" EXCEDE {g.Exceso} kg" : "";
                x.Span($"{(i > 0 ? " · " : "")}{g.Nombre} {string.Join("+", g.Ejes)} = {Kg(g.Kilogramos)} (lím. {g.Limite}){exceso}")
                    .FontColor(exceso.Length > 0 ? Colors.Red.Medium : Colors.Black);
            }
            if (marcarExceso && d.ExcesoTotal > 0) x.Span($" · TOTAL EXCEDE {d.ExcesoTotal} kg (lím. {d.LimiteTotal})").FontColor(Colors.Red.Medium);
        });
    }

    private static void Peso(IContainer c, string etiqueta, int? kg, bool manual) =>
        c.Text(x =>
        {
            x.Span(etiqueta + " ").Bold().FontSize(8);
            x.Span(Kg(kg) + (manual ? " *" : "")).Bold().FontSize(12);
        });

    private static void PieBfs(IContainer c) =>
        c.Row(r =>
        {
            if (LogoBfs.Length > 0) r.ConstantItem(42).Height(22).Image(LogoBfs).FitArea();
            r.ConstantItem(8);
            r.RelativeItem().AlignMiddle().Text(x =>
            {
                x.Span("Desarrollado por: ").Italic().FontSize(5.5f);
                x.Span(Desarrollado).FontSize(5.5f);
            });
        });

    private static void Aduana(IContainer c, TicketDatos t)
    {
        var p = t.Pesada;
        var a = p.Aduana!;
        c.Column(col =>
        {
            col.Spacing(6);
            col.Item().Row(r =>
            {
                r.RelativeItem().Element(x => Empresa(x, t));
                r.ConstantItem(150).Border(1).Padding(6).AlignCenter().AlignMiddle().Column(k =>
                {
                    k.Item().AlignCenter().Text("TICKET DE PESADA").Bold().FontSize(9);
                    k.Item().AlignCenter().Text($"Nº {p.TicketNumero}").Bold().FontSize(9);
                    if (t.EsAnulada) k.Item().AlignCenter().Text("ANULADA").Bold().FontColor(Colors.Red.Medium);
                    else if (t.EsPendiente) k.Item().AlignCenter().Text("PENDIENTE DE SALIDA").Bold().FontColor(Colors.Orange.Darken2);
                });
            });
            col.Item().LineHorizontal(1);

            Seccion(col, null, s =>
            {
                Par(s, "Fecha:", t.EntradaLocal.ToString("dd/MM/yyyy"));
                Par(s, "Hora:", t.EntradaLocal.ToString("HH:mm:ss"));
                Par(s, "Exportador/Importador Razón Social:", Texto(a.Exportador));
                Par(s, "CUIT:", Texto(a.ExportadorCuit));
                Par(s, "Escenario:", a.Escenario);
            });
            Seccion(col, "Destinación/Operación Aduanera", s =>
            {
                Par(s, "Número:", Texto(a.OperacionId));
                Par(s, "Mercadería:", Texto(p.Producto));
                Par(s, "Cód. Prod.:", Texto(t.ProductoCodigo));
                Par(s, "Código Aduana:", Texto(t.Aduana?.Codigo));
                Par(s, "Código LOT:", Texto(t.Aduana?.Lot));
                Par(s, "Certificado de Habilitación N°:", $"{Texto(t.Aduana?.Certificado)}   Vencimiento: {Texto(t.Aduana?.CertificadoVence)}");
            });
            Seccion(col, "Transportista", s =>
            {
                Par(s, "Razón Social:", Texto(p.Transporte));
                Par(s, "CUIT:", Texto(t.TransporteCuit));
            });
            Seccion(col, "Chofer", s =>
            {
                Par(s, "Apellido y Nombres:", Texto(p.Conductor));
                Par(s, "Nacionalidad:", Texto(a.ChoferNacionalidad));
                Par(s, "Tipo Documento:", Texto(a.ChoferTipoDocumento ?? (p.Documento != null ? "DNI" : null)));
                Par(s, "N° Documento:", Texto(p.Documento));
            });
            Seccion(col, "Vehículo", s =>
            {
                Par(s, "Patente Tractor:", Texto(p.PatenteChasis));
                Par(s, "Patente Acoplado:", Texto(p.PatenteAcoplado));
                Par(s, "País Procedencia:", Texto(p.Procedencia));
                Par(s, "País Destino:", Texto(p.Destino));
            });
            Seccion(col, "Pesada", s =>
            {
                Par(s, "Entrada:", Fecha(t.EntradaLocal));
                Par(s, "Salida:", Fecha(t.SalidaLocal));
                Par(s, "Peso Bruto:", Kg(p.Bruto));
                Par(s, "Peso Tara:", Kg(p.Tara));
                if (p.TaraPallets > 0) Par(s, "Tara pallets:", Kg(p.TaraPallets));
                Par(s, "Peso Neto:", t.EsAnulada ? "-" : Kg(p.Neto));
            });
            col.Item().Row(r =>
            {
                r.Spacing(8);
                r.RelativeItem().Border(0.5f).Padding(6).Column(s =>
                {
                    s.Item().Text("Contenedor").Bold();
                    Par(s, "Identificación:", Texto(a.ContenedorId));
                    Par(s, "Peso Bruto (kg.):", a.ContenedorBruto?.ToString() ?? "");
                    Par(s, "Peso Tara (kg.):", a.ContenedorTara.ToString());
                    Par(s, "Peso Neto (kg.):", a.ContenedorNeto?.ToString() ?? "");
                });
                r.RelativeItem().Border(0.5f).Padding(6).Column(s =>
                {
                    s.Item().Text("Bulto").Bold();
                    Par(s, "Identificación:", Texto(a.BultoId));
                    Par(s, "Peso Bruto (kg.):", a.BultoBruto?.ToString() ?? "");
                    Par(s, "Peso Tara (kg.):", a.BultoTara?.ToString() ?? "");
                });
            });
            Seccion(col, "Observaciones", s => s.Item().Text(Texto(p.Observaciones)));
            if (t.EsAnulada) col.Item().Text($"Anulada por {p.AnuladaPor}: {p.MotivoAnulacion}").FontColor(Colors.Red.Medium);
            col.Item().PaddingTop(6).Element(PieBfs);
        });
    }

    private static void Seccion(ColumnDescriptor col, string? titulo, Action<ColumnDescriptor> contenido) =>
        col.Item().Border(0.5f).Padding(6).Column(s =>
        {
            if (titulo != null) s.Item().Text(titulo).Bold().FontSize(8);
            contenido(s);
        });

    private static void Par(ColumnDescriptor s, string etiqueta, string valor) =>
        s.Item().Text(x =>
        {
            x.Span(etiqueta + " ").Bold();
            x.Span(valor);
        });

    private static byte[] CargarLogoBfs()
    {
        using var stream = typeof(TicketPdf).Assembly.GetManifestResourceStream("bfs_logo.png");
        if (stream == null) return [];
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
