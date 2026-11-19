using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;

namespace Bfs.Integrations.Centum;

/// <summary>
/// Centum (INAGRO): POST del JSON de la pesada cerrada al webhook configurado. 200/201 = enviado.
/// La 3.x lo mandaba una sola vez, al cerrar, y si fallaba se perdía; acá queda en el outbox y se reintenta.
/// </summary>
public sealed class CentumDestino : IDestinoIntegracion
{
    public const string Destino = "centum";

    /// <summary>Centum recibe la pesada completa, una vez: al cerrarla.</summary>
    public const string Evento = "SALIDA";

    public string Nombre => Destino;

    public bool Habilitado(IReadOnlyDictionary<string, string> c) =>
        c.GetValueOrDefault(ConfigKeys.CentumEnabled) == "true"
        && !string.IsNullOrWhiteSpace(c.GetValueOrDefault(ConfigKeys.CentumEndpoint));

    public async Task<Envio> EnviarAsync(OutboxMessage m, IReadOnlyDictionary<string, string> c, HttpClient http, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, c[ConfigKeys.CentumEndpoint])
        {
            Content = new StringContent(m.PayloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        var code = (int)response.StatusCode;
        return code switch
        {
            >= 200 and < 300 => new Envio(ResultadoEnvio.Enviado),
            400 or 422 => new Envio(ResultadoEnvio.Rechazado, $"{code}: {Recortar(await response.Content.ReadAsStringAsync(ct))}"),
            401 or 403 or 404 => new Envio(ResultadoEnvio.Rechazado, $"{code}: revisar la dirección del webhook de Centum"),
            _ => new Envio(ResultadoEnvio.Reintentar, $"Centum respondió {code}")
        };
    }

    private static string Recortar(string s) => s.Length > 500 ? s[..500] + "…" : s;
}

public static class CentumOutbox
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Centum activo y la empresa tiene una sociedad que vincula (no vacía ni "4").</summary>
    public static bool Envia(IReadOnlyDictionary<string, string> configs, string? codigoSociedad) =>
        configs.GetValueOrDefault(ConfigKeys.CentumEnabled) == "true" && Inagro.SociedadEnvia(codigoSociedad);

    /// <summary>Encola la pesada cerrada. Se guarda junto con la pesada.</summary>
    public static void Encolar(BfsDbContext db, IReadOnlyDictionary<string, string> configs, Pesada pesada, string? codigoSociedad, DateTime entradaLocal)
    {
        if (!Envia(configs, codigoSociedad)) return;
        var now = DateTimeOffset.UtcNow;
        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Destino = CentumDestino.Destino,
            PesadaId = pesada.Id,
            TicketNumero = pesada.TicketNumero,
            Evento = CentumDestino.Evento,
            PayloadJson = JsonSerializer.Serialize(CentumMapper.FromPesada(pesada, codigoSociedad, entradaLocal), Json),
            Estado = EstadosOutbox.Pendiente,
            CreadoEn = now,
            ProximoIntento = now
        });
    }
}
