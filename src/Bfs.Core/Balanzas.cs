namespace Bfs.Core;

public static class ProtocolosBalanza
{
    /// <summary>Trama entre STX (0x02) y ETX (0x03).</summary>
    public const string Stx = "STX";

    /// <summary>Una trama por línea (CR, LF o CRLF).</summary>
    public const string Line = "Line";

    public static IReadOnlyList<string> Todos { get; } = [Stx, Line];
}

public static class ModosBalanza
{
    public const string Serie = "serial";
    public const string Simulada = "sim";
}

/// <summary>Lo que necesita el lector para abrir y leer una balanza.</summary>
public record BalanzaConfig(
    Guid Id,
    string Nombre,
    string Modo,
    string Puerto,
    int Baudios,
    int DataBits,
    string Paridad,
    int StopBits,
    string Protocolo,
    string? Extraccion);

/// <param name="Estado">abierta, sin señal, error, simulada o cerrada.</param>
/// <param name="Estable">El peso no cambió durante el tiempo de estabilidad.</param>
public record LecturaBalanza(
    Guid BalanzaId,
    string Nombre,
    int Kilogramos,
    bool Estable,
    string Estado,
    string Puerto,
    DateTimeOffset En,
    string? Error = null)
{
    public static LecturaBalanza Ninguna { get; } = new(Guid.Empty, "", 0, false, "sin balanza", "", DateTimeOffset.MinValue);
}

/// <summary>Las balanzas conectadas a este nodo.</summary>
public interface IBalanzas
{
    IReadOnlyList<LecturaBalanza> Lecturas { get; }

    /// <summary>La lectura de esa balanza, o la de la primera si no se indica.</summary>
    LecturaBalanza Lectura(Guid? balanzaId = null);

    /// <summary>Últimas tramas recibidas tal cual (para configurar la extracción).</summary>
    IReadOnlyList<string> UltimasTramas(Guid balanzaId);

    IReadOnlyList<string> Puertos();

    /// <summary>Cierra todo y vuelve a abrir con esta configuración.</summary>
    void Aplicar(IReadOnlyList<BalanzaConfig> balanzas);
}
