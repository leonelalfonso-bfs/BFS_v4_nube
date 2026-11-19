namespace Bfs.Core.Entities;

public class Transporte : EntidadSync
{
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }

    /// <summary>Agente de Transporte Aduanero (operaciones de aduana).</summary>
    public bool EsAta { get; set; }
    public string? CodigoAta { get; set; }

    // Los usa el remito preimpreso de INAGRO (en la 3.x eran columnas de transports).
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? CondicionIva { get; set; }
}
