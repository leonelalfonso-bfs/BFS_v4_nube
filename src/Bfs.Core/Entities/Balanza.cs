namespace Bfs.Core.Entities;

/// <summary>
/// Una balanza conectada al nodo (tabla <c>balanzas</c> de la 3.x). Copia los parámetros del modelo al elegirlo,
/// pero se pueden ajustar: hay indicadores del mismo modelo configurados distinto.
/// </summary>
public class Balanza : EntidadSync
{
    public string Nombre { get; set; } = "";
    public string Modo { get; set; } = ModosBalanza.Serie;
    public string Puerto { get; set; } = "";
    public string? Modelo { get; set; }
    public int Baudios { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Paridad { get; set; } = "None";
    public int StopBits { get; set; } = 1;
    public string Protocolo { get; set; } = ProtocolosBalanza.Stx;
    public string? Extraccion { get; set; }
    public int Orden { get; set; }
    public bool Activa { get; set; } = true;

    public BalanzaConfig ToConfig() =>
        new(Id, Nombre, Modo, Puerto, Baudios, DataBits, Paridad, StopBits, Protocolo, Extraccion);
}
