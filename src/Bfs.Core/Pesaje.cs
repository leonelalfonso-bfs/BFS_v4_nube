namespace Bfs.Core;

public static class EstadosPesada
{
    /// <summary>Entró y espera la salida (pendiente).</summary>
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";
    public const string Anulada = "ANULADA";
}

public static class ModosPesaje
{
    /// <summary>Entrada y salida en dos pasos.</summary>
    public const string Normal = "Normal";

    /// <summary>Un solo paso: bruto de la balanza, tara guardada del vehículo o carro.</summary>
    public const string TaraConocida = "TaraConocida";
}

public static class TiposOperacion
{
    public const string Normal = "Normal";
    public const string Aduana = "Aduana";
}

public static class EscenariosAduana
{
    public const string IngresaConContenedor = "Ingresa con Contenedor";
    public const string CargaContenedorLleno = "Carga Contenedor Lleno";

    public static IReadOnlyList<string> Todos { get; } = [IngresaConContenedor, CargaContenedorLleno];

    /// <summary>
    /// Neto y bruto del contenedor al cerrar la pesada, con la misma regla que la 3.x:
    /// si el camión entra con el contenedor, el neto de la pesada es el del contenedor;
    /// si lo carga lleno, el neto de la pesada es el bruto del contenedor.
    /// </summary>
    public static (int Bruto, int Neto)? Contenedor(string? escenario, int netoPesada, int taraContenedor) => escenario switch
    {
        IngresaConContenedor => (netoPesada + taraContenedor, netoPesada),
        CargaContenedorLleno => (netoPesada, netoPesada - taraContenedor),
        _ => null
    };
}

public static class Tickets
{
    /// <summary>"01-000123": el puesto adelante para que dos puestos (o uno sin internet) nunca repitan número.</summary>
    public static string Formatear(int puesto, int numero) => $"{puesto:D2}-{numero:D6}";
}
