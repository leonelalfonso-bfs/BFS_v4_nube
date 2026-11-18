using System.Reflection;

namespace Bfs.Core;

/// <summary>
/// Qué versión del sistema es esta instalación.
/// Completa: varias empresas, integraciones (WORMS) y nube.
/// Estándar: para el cliente que solo pesa: una empresa, pesaje normal y aduana, todo local, sin API ni nube.
/// </summary>
public enum Edicion
{
    Completa,
    Estandar
}

public static class Ediciones
{
    /// <summary>
    /// La que se eligió al compilar (<c>dotnet publish -p:BfsEdicion=Estandar</c>). Queda grabada en el programa:
    /// una Estándar no se convierte en Completa tocando un archivo de configuración.
    /// </summary>
    public static Edicion Compilada { get; } =
        typeof(Ediciones).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BfsEdicion")?.Value == nameof(Edicion.Estandar)
            ? Edicion.Estandar
            : Edicion.Completa;

    /// <summary>La configuración solo puede achicar la edición (lo usan los tests), nunca agrandarla.</summary>
    public static Edicion Efectiva(Edicion configurada) =>
        Compilada == Edicion.Estandar ? Edicion.Estandar : configurada;

    /// <summary>Carpeta de datos por defecto: cada edición la suya, así las dos pueden estar en la misma PC.</summary>
    public static string CarpetaDatos(Edicion e) => e == Edicion.Estandar ? "bfs-4-estandar" : "bfs-4";

    /// <summary>Puerto local de la aplicación de escritorio.</summary>
    public static string UrlLocal(Edicion e) => e == Edicion.Estandar ? "http://127.0.0.1:5081" : "http://127.0.0.1:5080";

    public static string Nombre(Edicion e) => e == Edicion.Estandar ? "Sistema de Pesaje BFS 4.0 Estándar" : "Sistema de Pesaje BFS 4.0";
}
