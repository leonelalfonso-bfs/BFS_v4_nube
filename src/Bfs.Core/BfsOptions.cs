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
}

/// <summary>Identidad del puesto que genera los datos. En la nube no hay nodo.</summary>
public record NodoInfo(Guid? Id)
{
    public static NodoInfo Ninguno { get; } = new((Guid?)null);
}
