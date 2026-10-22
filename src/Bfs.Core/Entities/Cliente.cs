namespace Bfs.Core.Entities;

public class Cliente
{
    public Guid Id { get; set; }
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }
}
