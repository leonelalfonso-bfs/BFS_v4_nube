using System.Text.Json;

namespace Bfs.Core;

public record FieldDef(string Key, string Label, bool Visible, bool Required);

public static class FieldCatalog
{
    public static IReadOnlyList<FieldDef> Defaults { get; } =
    [
        // Oculto por defecto: es el tipo de movimiento para las integraciones (WORMS), no entrada / salida.
        new("tipo", "Tipo de movimiento (ingreso/egreso)", false, false),
        new("patenteChasis", "Patente chasis", true, true),
        new("patenteAcoplado", "Patente acoplado", true, false),
        new("conductor", "Conductor", true, false),
        new("documento", "Documento", true, false),
        new("tipoDocumento", "Tipo de documento", true, false),
        new("nacionalidad", "Nacionalidad", true, false),
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
            if (parsed is not { Count: > 0 }) return Defaults.ToList();
            // Campos agregados en versiones nuevas: aparecen con su valor por defecto en las instalaciones existentes.
            parsed.AddRange(Defaults.Where(d => parsed.All(f => f.Key != d.Key)));
            return parsed;
        }
        catch
        {
            return Defaults.ToList();
        }
    }

    public static string DefaultJson() =>
        JsonSerializer.Serialize(Defaults, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
