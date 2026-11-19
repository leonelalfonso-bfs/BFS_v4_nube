using System.Reflection;

namespace Bfs.Core;

/// <summary>
/// Qué versión del sistema es esta instalación.
/// Completa: varias empresas, integraciones (WORMS) y nube.
/// Estándar: para el cliente que solo pesa: una empresa, pesaje normal y aduana, todo local, sin API ni nube.
/// Inagro: la Completa más lo de INAGRO (campos de Centum, tanques, sucursales, remito preimpreso, envío a Centum)
/// y una sola numeración de tickets para todas sus empresas (sociedades).
/// </summary>
public enum Edicion
{
    Completa,
    Estandar,
    Inagro
}

public static class Ediciones
{
    /// <summary>
    /// La que se eligió al compilar (<c>dotnet publish -p:BfsEdicion=Estandar</c>). Queda grabada en el programa:
    /// una Estándar no se convierte en Completa tocando un archivo de configuración.
    /// </summary>
    public static Edicion Compilada { get; } =
        typeof(Ediciones).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BfsEdicion")?.Value switch
        {
            nameof(Edicion.Estandar) => Edicion.Estandar,
            nameof(Edicion.Inagro) => Edicion.Inagro,
            _ => Edicion.Completa
        };

    /// <summary>
    /// La configuración solo puede achicar la edición (lo usan los tests), nunca agrandarla. Desde la Completa se puede
    /// elegir la de INAGRO (es la Completa con lo de ese cliente); una INAGRO solo puede bajar a Estándar.
    /// </summary>
    public static Edicion Efectiva(Edicion configurada) => Efectiva(Compilada, configurada);

    public static Edicion Efectiva(Edicion compilada, Edicion configurada) => compilada switch
    {
        Edicion.Estandar => Edicion.Estandar,
        Edicion.Inagro => configurada == Edicion.Estandar ? Edicion.Estandar : Edicion.Inagro,
        _ => configurada
    };

    /// <summary>La Estándar no tiene API para otros sistemas, outbox ni nube; las demás sí.</summary>
    public static bool TieneIntegraciones(Edicion e) => e != Edicion.Estandar;

    /// <summary>INAGRO numera todas sus empresas con un mismo contador, como hacía su 3.x.</summary>
    public static bool NumeracionCompartida(Edicion e) => e == Edicion.Inagro;

    /// <summary>Carpeta de datos por defecto: cada edición la suya, así pueden estar en la misma PC.</summary>
    public static string CarpetaDatos(Edicion e) => e switch
    {
        Edicion.Estandar => "bfs-4-estandar",
        Edicion.Inagro => "bfs-4-inagro",
        _ => "bfs-4"
    };

    /// <summary>Puerto local de la aplicación de escritorio.</summary>
    public static string UrlLocal(Edicion e) => e switch
    {
        Edicion.Estandar => "http://127.0.0.1:5081",
        Edicion.Inagro => "http://127.0.0.1:5082",
        _ => "http://127.0.0.1:5080"
    };

    public static string Nombre(Edicion e) => e switch
    {
        Edicion.Estandar => "Sistema de Pesaje BFS 4.0 Estándar",
        Edicion.Inagro => "Sistema de Pesaje BFS 4.0 INAGRO",
        _ => "Sistema de Pesaje BFS 4.0"
    };

    /// <summary>Nombre del mutex de instancia única: cada edición el suyo.</summary>
    public static string Mutex(Edicion e) => e switch
    {
        Edicion.Estandar => @"Global\BFS-Pesaje-4-Estandar",
        Edicion.Inagro => @"Global\BFS-Pesaje-4-Inagro",
        _ => @"Global\BFS-Pesaje-4"
    };
}
