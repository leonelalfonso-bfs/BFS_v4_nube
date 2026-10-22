using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bfs.Integrations;

public class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<OutboxDispatcher> _log;

    public OutboxDispatcher(IServiceScopeFactory scopes, IHttpClientFactory http, ILogger<OutboxDispatcher> log)
    {
        _scopes = scopes;
        _http = http;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error en outbox");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task DispatchOnce(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
        var configs = await db.Configuraciones.Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);

        if (!configs.TryGetValue(ConfigKeys.WormsEnabled, out var enabled) || enabled != "true")
        {
            return;
        }

        var endpoint = configs.GetValueOrDefault(ConfigKeys.WormsEndpoint, "");
        var apiKey = configs.GetValueOrDefault(ConfigKeys.WormsApiKey, "");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        var pending = await db.Outbox
            .Where(o => o.Destino == "worms" && o.Estado == "pendiente" && o.ProximoIntento <= DateTimeOffset.UtcNow)
            .OrderBy(o => o.CreadoEn)
            .Take(10)
            .ToListAsync(ct);

        var client = _http.CreateClient("worms");
        client.Timeout = TimeSpan.FromSeconds(15);

        foreach (var item in pending)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(item.PayloadJson, Encoding.UTF8, "application/json")
                };
                request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.SendAsync(request, ct);
                var code = (int)response.StatusCode;

                if (code is 200 or 409)
                {
                    item.Estado = "enviado";
                    item.UltimoError = null;
                }
                else if (code is 400 or 401)
                {
                    item.Estado = "error";
                    item.UltimoError = $"{code}: {await response.Content.ReadAsStringAsync(ct)}";
                    _log.LogWarning("WORMS rechazó {Ticket} {Evento}: {Error}", item.TicketNumero, item.Evento, item.UltimoError);
                }
                else
                {
                    item.Intentos++;
                    item.ProximoIntento = DateTimeOffset.UtcNow.AddMinutes(1);
                    item.UltimoError = $"{code}";
                }
            }
            catch (Exception ex)
            {
                item.Intentos++;
                item.ProximoIntento = DateTimeOffset.UtcNow.AddMinutes(1);
                item.UltimoError = ex.Message;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
