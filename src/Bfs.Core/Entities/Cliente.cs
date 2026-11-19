namespace Bfs.Core.Entities;

public class Cliente : EntidadSync
{
    public string RazonSocial { get; set; } = "";
    public string? Cuit { get; set; }

    // Los usa el remito preimpreso de INAGRO (en la 3.x eran columnas de clients).
    public string? Direccion { get; set; }
    public string? Localidad { get; set; }
    public string? CondicionIva { get; set; }
}
