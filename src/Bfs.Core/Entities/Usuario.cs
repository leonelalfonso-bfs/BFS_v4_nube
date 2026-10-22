namespace Bfs.Core.Entities;

public class Usuario : EntidadSync
{
    /// <summary>Login. Se guarda en minúsculas y es único en todo el sistema.</summary>
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = Roles.Operador;
    public string Nombre { get; set; } = "";
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    /// <summary>Solo para el rol cliente: qué pesadas puede ver.</summary>
    public Guid? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public bool Activo { get; set; } = true;

    public static string NormalizarEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
}
