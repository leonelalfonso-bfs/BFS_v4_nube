namespace Bfs.Core.Entities;

public class Configuracion : EntidadSync
{
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public Guid? EmpresaId { get; set; }
}
