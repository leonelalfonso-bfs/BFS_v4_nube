using System.Globalization;
using System.Text;
using static Bfs.Documents.Formato;

namespace Bfs.Documents;

/// <summary>
/// Ticket para impresora térmica en ESC/POS, como <c>ThermalTicketGenerator.java</c>:
/// sin acentos (las térmicas baratas no los traen), 32 columnas en papel de 58 mm y 48 en 80 mm.
/// </summary>
public static class TicketTermico
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    public static int Columnas(string? anchoMm) => anchoMm?.Trim() == "58" ? 32 : 48;

    public static byte[] Generar(TicketDatos t, string? anchoMm)
    {
        var ancho = Columnas(anchoMm);
        var p = t.Pesada;
        var o = new MemoryStream();

        o.Write([Esc, (byte)'@']);           // reiniciar
        o.Write([Esc, (byte)'t', 0x00]);     // tabla PC437
        Centrado(o, true);
        Negrita(o, true);
        Linea(o, t.Empresa.RazonSocial, ancho);
        Negrita(o, false);
        if (t.Empresa.Cuit != null) Linea(o, $"CUIT: {t.Empresa.Cuit}", ancho);
        if (t.Empresa.Direccion != null) Linea(o, t.Empresa.Direccion, ancho);
        Linea(o, new string('-', ancho), ancho);

        Negrita(o, true);
        o.Write([Gs, (byte)'!', 0x11]);      // doble alto y ancho
        Linea(o, p.TicketNumero, ancho / 2);
        o.Write([Gs, (byte)'!', 0x00]);
        Negrita(o, false);
        Linea(o, t.EsTaraConocida ? "TARA CONOCIDA" : t.EsAduana ? "ADUANA" : "PESAJE NORMAL", ancho);
        if (t.EsAnulada) Linea(o, "*** ANULADA ***", ancho);
        else if (t.EsPendiente) Linea(o, "PENDIENTE DE SALIDA", ancho);

        Centrado(o, false);
        Linea(o, new string('-', ancho), ancho);
        Campo(o, "Entrada", Fecha(t.EntradaLocal), ancho);
        Campo(o, "Salida", Fecha(t.SalidaLocal), ancho);
        Campo(o, "Patente", p.PatenteChasis + (p.PatenteAcoplado != null ? " / " + p.PatenteAcoplado : ""), ancho);
        if (p.Conductor != null) Campo(o, "Chofer", p.Conductor, ancho);
        if (p.Cliente != null) Campo(o, "Cliente", p.Cliente, ancho);
        if (p.Transporte != null) Campo(o, "Transp.", p.Transporte, ancho);
        if (p.Producto != null) Campo(o, "Producto", p.Producto, ancho);
        if (p.Comprobante != null) Campo(o, "Remito", p.Comprobante, ancho);
        Linea(o, new string('-', ancho), ancho);

        if (t.EsTaraConocida)
        {
            Campo(o, "Bruto", Kg(p.PesoEntrada), ancho);
            Campo(o, "Tara", Kg(p.PesoSalida), ancho);
        }
        else
        {
            Campo(o, "Peso entrada", Kg(p.PesoEntrada) + (p.EntradaManualPor != null ? " *" : ""), ancho);
            Campo(o, "Peso salida", Kg(p.PesoSalida) + (p.SalidaManualPor != null ? " *" : ""), ancho);
        }
        if (p.TaraPallets > 0) Campo(o, "Tara pallets", Kg(p.TaraPallets), ancho);
        Negrita(o, true);
        Campo(o, "NETO", t.EsAnulada ? "-" : Kg(p.Neto), ancho);
        Negrita(o, false);
        if (p.Aduana?.ContenedorNeto is { } contNeto) Campo(o, "Neto contenedor", Kg(contNeto), ancho);
        if (p.EntradaManualPor != null || p.SalidaManualPor != null) Linea(o, "* peso cargado manualmente", ancho);

        if (!string.IsNullOrWhiteSpace(t.Pie))
        {
            Linea(o, "", ancho);
            Centrado(o, true);
            Linea(o, t.Pie, ancho);
        }
        o.Write([Esc, (byte)'d', 0x04]);     // avanzar 4 líneas
        o.Write([Gs, (byte)'V', 0x42, 0x00]); // corte parcial
        return o.ToArray();
    }

    private static void Centrado(Stream o, bool si) => o.Write([Esc, (byte)'a', (byte)(si ? 1 : 0)]);
    private static void Negrita(Stream o, bool si) => o.Write([Esc, (byte)'E', (byte)(si ? 1 : 0)]);

    /// <summary>"Etiqueta ........ valor" en una línea; si no entra, el valor va abajo.</summary>
    private static void Campo(Stream o, string etiqueta, string valor, int ancho)
    {
        etiqueta = Ascii(etiqueta);
        valor = Ascii(valor);
        if (etiqueta.Length + valor.Length + 1 <= ancho)
        {
            Linea(o, etiqueta + new string(' ', ancho - etiqueta.Length - valor.Length) + valor, ancho);
        }
        else
        {
            Linea(o, etiqueta + ":", ancho);
            Linea(o, "  " + valor, ancho);
        }
    }

    private static void Linea(Stream o, string texto, int ancho)
    {
        var t = Ascii(texto);
        if (t.Length > ancho) t = t[..ancho];
        o.Write(Encoding.ASCII.GetBytes(t + "\n"));
    }

    /// <summary>"Pérez Ñandú" → "Perez Nandu".</summary>
    public static string Ascii(string? texto)
    {
        var sinMarcas = new string((texto ?? "").Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return new string(sinMarcas.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
    }
}
