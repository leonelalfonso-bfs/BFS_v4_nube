namespace Bfs.Core.Entities;

/// <summary>Procedencia o destino frecuente (edición Inagro). En una orden de traspaso son las sucursales desde / hacia.</summary>
public class Sucursal : EntidadSync
{
    public string Nombre { get; set; } = "";
}
