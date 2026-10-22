using System.Globalization;
using System.Text.RegularExpressions;

namespace Bfs.Scale.Tramas;

/// <summary>
/// Saca los kilos de una trama. La extracción es <c>inicio:fin</c> (posiciones como <c>substring</c>, fin excluido),
/// la misma que guardaba la 3.x en <c>balanza_modelos.regex</c> (ej. Moretti W-180 = "5:10").
/// </summary>
public static partial class ExtractorPeso
{
    public static int? Extraer(string? trama, string? extraccion)
    {
        if (string.IsNullOrEmpty(trama)) return null;

        if (Rango(extraccion) is var (inicio, fin))
        {
            // Primero sobre la trama cruda; si el ruido corrió los caracteres, sobre la trama limpia (como la 3.x).
            var valor = Recortar(trama, inicio, fin) ?? Recortar(Limpia().Replace(trama, ""), inicio, fin);
            if (valor != null) return valor;
        }

        // Sin extracción (o sin dígitos en el rango): todos los dígitos de la trama.
        return Numero(Limpia().Replace(trama, ""));
    }

    /// <returns>(inicio, fin) si la extracción es válida.</returns>
    public static (int Inicio, int Fin)? Rango(string? extraccion)
    {
        var partes = (extraccion ?? "").Split(':');
        return partes.Length == 2
               && int.TryParse(partes[0].Trim(), out var inicio)
               && int.TryParse(partes[1].Trim(), out var fin)
               && inicio >= 0 && fin > inicio
            ? (inicio, fin)
            : null;
    }

    private static int? Recortar(string trama, int inicio, int fin)
    {
        var finReal = Math.Min(fin, trama.Length);
        return finReal > inicio ? Numero(trama[inicio..finReal]) : null;
    }

    /// <summary>
    /// Dígitos y punto decimal. A diferencia de la 3.x, un signo menos dentro del rango se respeta
    /// (la 3.x lo descartaba y un peso negativo se leía como positivo).
    /// </summary>
    private static int? Numero(string texto)
    {
        var digitos = SoloNumero().Replace(texto, "");
        if (digitos.Length == 0 || !digitos.Any(char.IsDigit)) return null;
        if (!double.TryParse(digitos, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var kg)) return null;
        // Negativo solo si el '-' está pegado antes del primer dígito: un guion en otra parte de la trama no es signo.
        var primerDigito = texto.IndexOfAny("0123456789".ToCharArray());
        var negativo = texto[..primerDigito].TrimEnd().EndsWith('-');
        return (int)Math.Round(negativo ? -kg : kg, MidpointRounding.AwayFromZero);
    }

    [GeneratedRegex("[^a-zA-Z0-9.\\-]")]
    private static partial Regex Limpia();

    [GeneratedRegex("[^0-9.]")]
    private static partial Regex SoloNumero();
}
