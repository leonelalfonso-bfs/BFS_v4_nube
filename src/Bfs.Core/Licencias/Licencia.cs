using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bfs.Core.Licencias;

/// <summary>
/// Licencia de un nodo de portería. La firma BFS con su clave privada; el sistema solo tiene la pública,
/// así que no se puede fabricar ni editar (a diferencia de la 3.x, donde era la MAC en texto plano).
/// </summary>
/// <param name="Maquina">Id de la PC (<c>MAC-XXXXXXXXXXXX</c>, mismo formato que la 3.x).</param>
/// <param name="Vence">Último instante válido. Nulo = sin vencimiento (modo Local). En modo Nube la renueva cada sincronización con 15 días de gracia.</param>
public record Licencia(string Cliente, string Maquina, DateTimeOffset Emitida, DateTimeOffset? Vence);

public static class LicenciaCodec
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Formato: <c>base64url(json).base64url(firma ECDSA P-256 / SHA-256)</c>.</summary>
    public static string Firmar(Licencia licencia, ECDsa clavePrivada)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(licencia, Json);
        var firma = clavePrivada.SignData(payload, HashAlgorithmName.SHA256);
        return $"{B64(payload)}.{B64(firma)}";
    }

    public static Licencia? Verificar(string? texto, ECDsa clavePublica)
    {
        var partes = (texto ?? "").Trim().Split('.');
        if (partes.Length != 2) return null;

        try
        {
            var payload = DeB64(partes[0]);
            var firma = DeB64(partes[1]);
            if (!clavePublica.VerifyData(payload, firma, HashAlgorithmName.SHA256)) return null;
            return JsonSerializer.Deserialize<Licencia>(payload, Json);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    public static ECDsa ClaveDesdePem(string pem)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa;
    }

    private static string B64(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DeB64(string s)
    {
        var b = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b.PadRight(b.Length + (4 - b.Length % 4) % 4, '='));
    }
}
