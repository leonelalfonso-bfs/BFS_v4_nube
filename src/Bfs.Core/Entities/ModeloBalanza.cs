namespace Bfs.Core.Entities;

/// <summary>Parámetros de fábrica de un indicador (tabla <c>balanza_modelos</c> de la 3.x).</summary>
public class ModeloBalanza : EntidadSync
{
    public string Nombre { get; set; } = "";
    public int Baudios { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Paridad { get; set; } = "None";
    public int StopBits { get; set; } = 1;
    public string Protocolo { get; set; } = ProtocolosBalanza.Stx;

    /// <summary><c>inicio:fin</c> dentro de la trama. Vacío = todos los dígitos.</summary>
    public string? Extraccion { get; set; }
}
