using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;

namespace Bfs.Api.Automatizacion;

/// <summary>Lo que el pesaje necesita de la automatización: sensores, fotos y la secuencia de salida.</summary>
public sealed class AutomatizacionPesaje(AutomatizacionService automatizacion, CamarasService camaras, FotosPesadas fotos)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    /// <summary>Motivo por el que no se puede tomar el peso de esa balanza (sensores), o null.</summary>
    public string? Bloqueo(Guid? balanzaId) => automatizacion.BloqueoCaptura(balanzaId);

    public bool SacaFotos
    {
        get
        {
            var c = automatizacion.Config;
            return c.Habilitada && c.CapturarFotos && c.Camaras.Any(x => x.Habilitada);
        }
    }

    public async Task<IReadOnlyList<FotoCamara>> FotosAsync() => SacaFotos ? await camaras.FotosAsync() : [];

    /// <summary>Guarda las fotos y las suma a las que ya tenía la pesada (las de la entrada, al cerrar la salida).</summary>
    public void Agregar(Pesada p, string momento, IReadOnlyList<FotoCamara>? tomadas)
    {
        if (tomadas is not { Count: > 0 }) return;
        var lista = Leer(p);
        lista.RemoveAll(f => f.Momento == momento);
        lista.AddRange(fotos.Guardar(p.Id, momento, tomadas));
        p.FotosJson = JsonSerializer.Serialize(lista, JsonWeb);
    }

    public Task AceptadaAsync(Guid? balanzaId) => automatizacion.PesadaAceptadaAsync(balanzaId);

    public static List<FotoPesada> Leer(Pesada p)
    {
        try { return JsonSerializer.Deserialize<List<FotoPesada>>(p.FotosJson, JsonWeb) ?? []; }
        catch (JsonException) { return []; }
    }
}
