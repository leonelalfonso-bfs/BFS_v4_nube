namespace Bfs.Core.Entities;

/// <summary>Datos de la operación aduanera de una pesada (columnas <c>aduana_*</c>, contenedor y bulto de la 3.x).</summary>
public class DatosAduana
{
    public string Escenario { get; set; } = "";
    public string? Exportador { get; set; }
    public string? ExportadorCuit { get; set; }
    public string? OperacionId { get; set; }
    public string? ChoferTipoDocumento { get; set; }
    public string? ChoferNacionalidad { get; set; }
    public string? ContenedorId { get; set; }
    public int ContenedorTara { get; set; }

    /// <summary>Se calculan al cerrar la pesada según el escenario.</summary>
    public int? ContenedorBruto { get; set; }
    public int? ContenedorNeto { get; set; }

    public string? BultoId { get; set; }
    public int? BultoBruto { get; set; }
    public int? BultoTara { get; set; }
}
