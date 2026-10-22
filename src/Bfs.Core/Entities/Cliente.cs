namespace Bfs.Core.Entities;

public class Cliente : EntidadSync
{
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }
}
