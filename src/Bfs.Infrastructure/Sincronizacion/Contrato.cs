using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bfs.Infrastructure.Sincronizacion;

/// <summary>Lo que manda la portería en cada sincronización.</summary>
/// <param name="Cambios">Por tipo, las filas que cambiaron en la portería desde el último envío.</param>
/// <param name="Desde">Por tipo, hasta qué secuencia de la nube ya recibió.</param>
public record PedidoSync(string Maquina, Dictionary<string, List<JsonElement>> Cambios, Dictionary<string, long> Desde);

/// <param name="Licencia">Licencia renovada por 15 días (null si la nube no tiene clave de licencias configurada).</param>
public record RespuestaSync(
    string Tenant,
    Dictionary<string, ResultadoAplicar> Aplicados,
    Dictionary<string, List<JsonElement>> Cambios,
    Dictionary<string, long> Hasta,
    string? Licencia);

public record PedidoActivacion(string? Codigo, string? Maquina);

public record RespuestaActivacion(Guid NodoId, string Token, string Tenant);

public static class SecretosNodo
{
    /// <summary>Máximo de filas por tipo en cada viaje: si hay más, siguen en la próxima vuelta.</summary>
    public const int Lote = 1000;

    /// <summary>
    /// Se vuelve a pedir/enviar un poco de lo anterior: dos transacciones que terminan en desorden no se pierden.
    /// Aplicar dos veces lo mismo no cambia nada.
    /// </summary>
    public static readonly TimeSpan Solapamiento = TimeSpan.FromSeconds(5);

    /// <summary>Código para activar una portería: 12 caracteres sin letras que se confundan (O/0, I/1), en grupos de 4.</summary>
    public static string NuevoCodigo()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var c = RandomNumberGenerator.GetString(alfabeto, 12);
        return $"{c[..4]}-{c[4..8]}-{c[8..]}";
    }

    public static string NuevoToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Hash del código de activación. Lo tipea una persona: se ignoran mayúsculas, espacios y guiones
    /// ("whza uwvf wbpz" = "WHZA-UWVF-WBPZ").
    /// </summary>
    public static string HashCodigo(string codigo) =>
        Sha(new string(codigo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()));

    /// <summary>Hash del token de la portería (lo maneja el programa: se compara exacto).</summary>
    public static string HashToken(string token) => Sha(token);

    public static bool TokenValido(string token, string? hash) =>
        hash != null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(HashToken(token)), Encoding.ASCII.GetBytes(hash));

    private static string Sha(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
