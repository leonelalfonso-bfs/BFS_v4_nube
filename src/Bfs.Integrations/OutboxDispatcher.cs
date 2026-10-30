using Bfs.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bfs.Integrations;

/// <summary>Cada 15 segundos manda lo pendiente del outbox a cada integración habilitada.</summary>
public class OutboxDispatcher(IServiceScopeFactory scopes, DespachadorOutbox despachador, ILogger<OutboxDispatcher> log) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await despachador.ProcesarAsync(scope.ServiceProvider.GetRequiredService<BfsDbContext>(), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Error en el outbox");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
