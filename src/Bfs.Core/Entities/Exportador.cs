namespace Bfs.Core.Entities;

/// <summary>Exportador de la operación aduanera (tabla <c>exportadores</c> de la 3.x).</summary>
public class Exportador : EntidadSync
{
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }
}
