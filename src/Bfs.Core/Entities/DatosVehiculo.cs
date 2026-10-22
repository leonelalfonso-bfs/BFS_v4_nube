namespace Bfs.Core.Entities;

/// <summary>
/// Últimos datos usados con una patente, para autocompletar la próxima entrada
/// (<c>weighing_data_cache</c> de la 3.x). Se actualiza solo en cada entrada.
/// </summary>
public class DatosVehiculo : EntidadSync
{
    public string PatenteChasis { get; set; } = "";
    public string? PatenteAcoplado { get; set; }
    public string? Conductor { get; set; }
    public string? Documento { get; set; }
    public string? TipoDocumento { get; set; }
    public string? Nacionalidad { get; set; }
    public string? Transporte { get; set; }
}
