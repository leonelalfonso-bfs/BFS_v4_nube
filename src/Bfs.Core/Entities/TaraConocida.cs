namespace Bfs.Core.Entities;

/// <summary>Tara guardada de un vehículo o carro, para pesar en un solo paso (tabla <c>taras</c> de la 3.x).</summary>
public class TaraConocida : EntidadSync
{
    /// <summary>Patente o número de carro, en mayúsculas y sin espacios.</summary>
    public string Identificador { get; set; } = "";
    public int Kilogramos { get; set; }
    public string? Descripcion { get; set; }
    public DateTimeOffset TomadaEn { get; set; }
}
