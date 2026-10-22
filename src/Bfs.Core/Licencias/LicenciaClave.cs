namespace Bfs.Core.Licencias;

/// <summary>
/// Clave pública de BFS para verificar licencias. Es pública a propósito: con ella solo se verifica, no se firma.
/// La privada está fuera del repositorio (ver docs/LICENCIAS.md).
/// </summary>
public static class LicenciaClave
{
    public const string PublicaPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEf5WM433u4ZFQWPbn46XNLzEVJYK4
        RxxU3rIKoeXjxfTpODYS505bQ4zaey4TpwJ1GbSXn5r4AXpZRes2FLXtbg==
        -----END PUBLIC KEY-----
        """;
}
