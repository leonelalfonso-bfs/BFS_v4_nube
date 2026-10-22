namespace Bfs.Core.Entities;

public class Empresa : EntidadSync
{
    public string Nombre { get; set; } = "";
    public string? Cuit { get; set; }
    public bool Activa { get; set; } = true;
    public int ProximoTicket { get; set; } = 1;
}
