namespace Bfs.Core.Entities;

public class Configuracion
{
    public Guid Id { get; set; }
    public string Clave { get; set; } = "";
    public string Valor { get; set; } = "";
    public Guid? EmpresaId { get; set; }
}
