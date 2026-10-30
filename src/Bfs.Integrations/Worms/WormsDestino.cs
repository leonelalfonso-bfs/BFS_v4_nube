using System.Net.Http.Headers;
using System.Text;
using Bfs.Core;
using Bfs.Core.Entities;

namespace Bfs.Integrations.Worms;

/// <summary>WORMS: POST del JSON de la pesada con <c>x-api-key</c>. 200 o 409 (ya lo tenía) = enviado.</summary>
public sealed class WormsDestino : IDestinoIntegracion
{
    public const string Destino = "worms";

    public string Nombre => Destino;

    public bool Habilitado(IReadOnlyDictionary<string, string> c) =>
        c.GetValueOrDefault(ConfigKeys.WormsEnabled) == "true"
        && !string.IsNullOrWhiteSpace(c.GetValueOrDefault(ConfigKeys.WormsEndpoint))
        && !string.IsNullOrWhiteSpace(c.GetValueOrDefault(ConfigKeys.WormsApiKey));

    public async Task<Envio> EnviarAsync(OutboxMessage m, IReadOnlyDictionary<string, string> c, HttpClient http, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, c[ConfigKeys.WormsEndpoint])
        {
            Content = new StringContent(m.PayloadJson, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("x-api-key", c[ConfigKeys.WormsApiKey]);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        var code = (int)response.StatusCode;
        return code switch
        {
            200 or 201 or 409 => new Envio(ResultadoEnvio.Enviado),
            400 or 401 or 403 or 422 => new Envio(ResultadoEnvio.Rechazado, $"{code}: {Recortar(await response.Content.ReadAsStringAsync(ct))}"),
            _ => new Envio(ResultadoEnvio.Reintentar, $"WORMS respondió {code}")
        };
    }

    private static string Recortar(string s) => s.Length > 500 ? s[..500] + "…" : s;
}
