using System.Text.Json;

namespace Bfs.Core;

/// <summary>
/// Cómo se tomó el peso de una pasada (entrada o salida).
/// Partes: balanza corta, el camión se pesa en dos o más veces (chasis, acoplado) y vale la suma; el detalle no importa.
/// Ejes: balanza de ejes, se pesa eje por eje y se controla cada grupo contra los límites de Vialidad, y el total.
/// </summary>
public static class ModosCaptura
{
    public const string Simple = "simple";
    public const string Partes = "partes";
    public const string Ejes = "ejes";
}

/// <param name="Ejes">Cuántos ejes tiene el grupo (se pesan uno por uno).</param>
/// <param name="LimiteKg">Límite por defecto del grupo; BFS lo puede cambiar en la configuración.</param>
public record TipoEje(string Id, string Nombre, int Ejes, int LimiteKg);

public static class TiposEje
{
    /// <summary>
    /// Valores de referencia (Ley 24.449, Anexo R). Se cargan por defecto y se editan en Configuración → General:
    /// verificarlos con la tabla vigente de Vialidad Nacional antes de usarlos para controlar.
    /// </summary>
    public static IReadOnlyList<TipoEje> Todos { get; } =
    [
        new("simple_simple", "Eje simple, rueda simple", 1, 6000),
        new("simple_doble", "Eje simple, rueda doble", 1, 10500),
        new("tandem_doble", "Tándem doble, rueda doble", 2, 18000),
        new("tandem_triple", "Tándem triple, rueda doble", 3, 25500)
    ];

    public const int LimiteTotalPorDefecto = 45000;

    public static TipoEje? Buscar(string? id) => Todos.FirstOrDefault(t => t.Id == id);

    /// <summary>Límites configurados (tipo → kg); lo que no esté configurado usa el valor por defecto.</summary>
    public static Dictionary<string, int> Limites(string? json)
    {
        var limites = Todos.ToDictionary(t => t.Id, t => t.LimiteKg);
        if (string.IsNullOrWhiteSpace(json)) return limites;
        try
        {
            foreach (var (id, kg) in JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? [])
            {
                if (limites.ContainsKey(id) && kg > 0) limites[id] = kg;
            }
        }
        catch (JsonException)
        {
        }
        return limites;
    }
}

/// <summary>Una captura de la pasada: una parte del camión o un eje.</summary>
public record PartePeso(string Etiqueta, int Kilogramos, bool Manual = false);

/// <summary>Un grupo de ejes (simple, tándem doble, tándem triple) con sus pesos y su límite.</summary>
public record GrupoEjes(string Tipo, string Nombre, List<int> Ejes, int Limite)
{
    public int Kilogramos => Ejes.Sum();
    public int Exceso => Math.Max(0, Kilogramos - Limite);
}

/// <summary>El detalle de una pasada pesada en partes o por ejes. El total es el peso de entrada o de salida.</summary>
public record PesoPorPartes(string Modo, List<PartePeso> Partes, List<GrupoEjes>? Grupos = null, int? LimiteTotal = null)
{
    public int Total => Partes.Sum(p => p.Kilogramos);
    public int ExcesoTotal => LimiteTotal is { } l ? Math.Max(0, Total - l) : 0;
    public bool HayExceso => ExcesoTotal > 0 || (Grupos?.Any(g => g.Exceso > 0) ?? false);
}

/// <summary>Lo que se guarda en la pesada (<c>PartesJson</c>): el detalle de cada pasada que no fue simple.</summary>
public record PartesPesada(PesoPorPartes? Entrada = null, PesoPorPartes? Salida = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PartesPesada Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<PartesPesada>(json, Json) ?? new(); }
        catch (JsonException) { return new(); }
    }

    public string Escribir() => JsonSerializer.Serialize(this, Json);

    public bool HayExceso => (Entrada?.HayExceso ?? false) || (Salida?.HayExceso ?? false);
}
