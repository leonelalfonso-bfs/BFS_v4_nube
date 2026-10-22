using System.Text.Json;

namespace Bfs.Core;

public record FieldDef(string Key, string Label, bool Visible, bool Required);

public static class FieldCatalog
{
    public static IReadOnlyList<FieldDef> Defaults { get; } =
    [
        new("tipo", "Tipo (ingreso/egreso)", true, true),
        new("patenteChasis", "Patente chasis", true, true),
        new("patenteAcoplado", "Patente acoplado", true, false),
        new("conductor", "Conductor", true, false),
        new("documento", "Documento", true, false),
        new("cliente", "Cliente", true, false),
        new("transporte", "Transporte", true, false),
        new("producto", "Producto", true, false),
        new("procedencia", "Procedencia", true, false),
        new("destino", "Destino", true, false),
        new("comprobante", "Remito", true, false),
        new("observaciones", "Observaciones", true, false),
        new("taraPallets", "Tara pallets (kg)", true, false)
    ];

    public static List<FieldDef> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Defaults.ToList();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<FieldDef>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return parsed is { Count: > 0 } ? parsed : Defaults.ToList();
        }
        catch
        {
            return Defaults.ToList();
        }
    }

    public static string DefaultJson() =>
        JsonSerializer.Serialize(Defaults, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
