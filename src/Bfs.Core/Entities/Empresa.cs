namespace Bfs.Core.Entities;

public class Empresa : EntidadSync
{
    /// <summary>Razón social. Sale en el ticket.</summary>
    public string Nombre { get; set; } = "";
    public string? Cuit { get; set; }
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? Provincia { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool Activa { get; set; } = true;
    public int ProximoTicket { get; set; } = 1;
}
