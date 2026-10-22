namespace Bfs.Core.Entities;

/// <summary>
/// Próximo número de ticket de una empresa en un puesto. Cada puesto solo toca su fila,
/// así dos nodos que sincronizan con la nube nunca se pisan el contador.
/// </summary>
public class ContadorTicket : EntidadSync
{
    public Guid EmpresaId { get; set; }
    public int Puesto { get; set; }
    public int Proximo { get; set; } = 1;
}
