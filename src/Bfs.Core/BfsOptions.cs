namespace Bfs.Core;

public enum ModoDespliegue
{
    /// <summary>PC de portería: SQLite local, balanza por COM, funciona sin internet.</summary>
    Nodo,

    /// <summary>Servidor central: PostgreSQL, sin balanza.</summary>
    Nube
}

/// <summary>Sección "Bfs" de appsettings.</summary>
public class BfsOptions
{
    public const string Seccion = "Bfs";

    public ModoDespliegue Modo { get; set; } = ModoDespliegue.Nodo;

    /// <summary>Carpeta de datos. Vacío = %ProgramData%\bfs-4.</summary>
    public string? DataPath { get; set; }

    /// <summary>Zona para mostrar fechas en tickets y Excel (en la base todo está en UTC).</summary>
    public string ZonaHoraria { get; set; } = "America/Argentina/Buenos_Aires";

    public DateTime HoraLocal(DateTimeOffset utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria)).DateTime;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utc.ToLocalTime().DateTime;
        }
    }
}

/// <summary>Identidad del puesto que genera los datos. En la nube no hay nodo.</summary>
public record NodoInfo(Guid? Id)
{
    public static NodoInfo Ninguno { get; } = new((Guid?)null);
}
