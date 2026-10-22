namespace Bfs.Core.Entities;

public class Transporte : EntidadSync
{
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }

    /// <summary>Agente de Transporte Aduanero (operaciones de aduana).</summary>
    public bool EsAta { get; set; }
    public string? CodigoAta { get; set; }
}
