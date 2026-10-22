namespace Bfs.Core.Entities;

public class Usuario : EntidadSync
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rol { get; set; } = Roles.Operador;
    public string Nombre { get; set; } = "";
    public Guid? EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public bool Activo { get; set; } = true;
}
