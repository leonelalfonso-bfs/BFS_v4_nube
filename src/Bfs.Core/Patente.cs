namespace Bfs.Core;

public static class Patente
{
    /// <summary>Mayúsculas y sin espacios ni guiones: "ab 123-cd" → "AB123CD".</summary>
    public static string Normalizar(string? patente) =>
        new((patente ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
