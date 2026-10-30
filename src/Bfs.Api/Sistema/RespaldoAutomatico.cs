using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Sistema;

/// <summary>
/// Cada 10 minutos: si hubo cambios, actualiza el espejo (la 3.x lo hacía después de cada pesada).
/// Una vez por día: backup automático en la carpeta de backups (se guardan los últimos 30).
/// </summary>
public class RespaldoAutomatico : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopes;
    private readonly BfsPaths _paths;
    private readonly ILogger<RespaldoAutomatico> _log;
    private DateTimeOffset? _ultimoCambioCopiado;

    public RespaldoAutomatico(IServiceScopeFactory scopes, BfsPaths paths, ILogger<RespaldoAutomatico> log)
    {
        _scopes = scopes;
        _paths = paths;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Un rato después de arrancar, para no competir con la apertura de balanzas.
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UnaVuelta();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error en el respaldo automático");
            }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task UnaVuelta()
    {
        using var scope = _scopes.CreateScope();
        var sistema = scope.ServiceProvider.GetRequiredService<SistemaService>();
        var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();

        var hoy = $"{SistemaService.PrefijoAutomatico}bfs-{DateTime.Now:yyyyMMdd}";
        if (!sistema.Backups().Any(b => b.Archivo.StartsWith(hoy, StringComparison.Ordinal)))
        {
            var b = sistema.CrearBackup(automatico: true);
            _log.LogInformation("Backup automático {Archivo}", b.Archivo);
        }

        var ultimoCambio = await db.Pesadas.IgnoreQueryFilters().MaxAsync(p => (DateTimeOffset?)p.ModificadoEn);
        if (ultimoCambio == _ultimoCambioCopiado) return;
        var config = await scope.ServiceProvider.GetRequiredService<Config.ConfigService>().GlobalAsync();
        if (string.IsNullOrWhiteSpace(config.GetValueOrDefault(ConfigKeys.EspejoCarpeta))) return;

        var error = await sistema.ActualizarEspejoAsync();
        if (error == null) _ultimoCambioCopiado = ultimoCambio;
        else _log.LogWarning("Espejo: {Error}", error);
    }
}
