using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bfs.Integrations;

public static class EstadosOutbox
{
    public const string Pendiente = "pendiente";
    public const string Enviado = "enviado";

    /// <summary>El destino lo rechazó (dato inválido, clave incorrecta): no se reintenta solo.</summary>
    public const string Error = "error";

    /// <summary>BFS decidió no mandarlo (ej. un rechazo que no tiene arreglo): deja pasar los siguientes de esa pesada.</summary>
    public const string Descartado = "descartado";
}

public enum ResultadoEnvio
{
    Enviado,

    /// <summary>Rechazo definitivo: hay que mirarlo.</summary>
    Rechazado,

    /// <summary>Falla pasajera (sin conexión, el destino caído): se reintenta solo.</summary>
    Reintentar
}

public record Envio(ResultadoEnvio Resultado, string? Detalle = null);

/// <summary>
/// Un sistema al que se le mandan las pesadas (WORMS, y Centum cuando llegue su documentación).
/// Cada uno arma su pedido; el despacho, los reintentos y el orden son comunes.
/// </summary>
public interface IDestinoIntegracion
{
    string Nombre { get; }
    bool Habilitado(IReadOnlyDictionary<string, string> config);
    Task<Envio> EnviarAsync(OutboxMessage mensaje, IReadOnlyDictionary<string, string> config, HttpClient http, CancellationToken ct);
}

/// <summary>
/// Manda lo pendiente del outbox. Reglas:
/// - Los envíos de una misma pesada salen en orden: si la ENTRADA está esperando, la SALIDA no se adelanta.
/// - Una falla pasajera se reintenta con espera creciente (1, 2, 4... hasta 30 minutos).
/// - Un rechazo queda en "error" para que BFS lo vea y lo reintente o lo descarte.
/// </summary>
public sealed class DespachadorOutbox(IEnumerable<IDestinoIntegracion> destinos, IHttpClientFactory http, ILogger<DespachadorOutbox> log, TimeProvider? clock = null)
{
    public const int Lote = 20;
    public static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(30);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public IReadOnlyList<IDestinoIntegracion> Destinos { get; } = destinos.ToList();

    public static TimeSpan Espera(int intentos) =>
        TimeSpan.FromMinutes(Math.Min(EsperaMaxima.TotalMinutes, Math.Pow(2, Math.Max(0, intentos - 1))));

    /// <returns>Cuántos mensajes se intentaron mandar.</returns>
    public async Task<int> ProcesarAsync(BfsDbContext db, CancellationToken ct = default)
    {
        var config = await db.Configuraciones.AsNoTracking().Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);
        var intentados = 0;
        foreach (var destino in Destinos.Where(d => d.Habilitado(config)))
        {
            var ahora = _clock.GetUtcNow();
            var candidatos = await db.Outbox
                .Where(o => o.Destino == destino.Nombre && o.Estado == EstadosOutbox.Pendiente && o.ProximoIntento <= ahora)
                .OrderBy(o => o.CreadoEn)
                .Take(Lote)
                .ToListAsync(ct);

            var cliente = http.CreateClient(destino.Nombre);
            cliente.Timeout = TimeSpan.FromSeconds(15);
            var trabadas = new HashSet<Guid>();

            foreach (var m in candidatos)
            {
                // Algo anterior de esta pesada todavía no salió (o fue rechazado): este espera.
                if (trabadas.Contains(m.PesadaId) || await db.Outbox.AnyAsync(o =>
                        o.Destino == m.Destino && o.PesadaId == m.PesadaId && o.CreadoEn < m.CreadoEn
                        && (o.Estado == EstadosOutbox.Pendiente || o.Estado == EstadosOutbox.Error), ct))
                {
                    trabadas.Add(m.PesadaId);
                    continue;
                }

                Envio envio;
                try
                {
                    envio = await destino.EnviarAsync(m, config, cliente, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    envio = new Envio(ResultadoEnvio.Reintentar, ex.Message);
                }

                intentados++;
                m.Intentos++;
                switch (envio.Resultado)
                {
                    case ResultadoEnvio.Enviado:
                        m.Estado = EstadosOutbox.Enviado;
                        m.UltimoError = null;
                        break;
                    case ResultadoEnvio.Rechazado:
                        m.Estado = EstadosOutbox.Error;
                        m.UltimoError = envio.Detalle;
                        trabadas.Add(m.PesadaId);
                        log.LogWarning("{Destino} rechazó el ticket {Ticket} ({Evento}): {Error}", destino.Nombre, m.TicketNumero, m.Evento, envio.Detalle);
                        break;
                    default:
                        m.UltimoError = envio.Detalle;
                        m.ProximoIntento = _clock.GetUtcNow().Add(Espera(m.Intentos));
                        trabadas.Add(m.PesadaId);
                        break;
                }
                // Uno por uno: si se corta la luz, lo ya enviado no se vuelve a mandar.
                await db.SaveChangesAsync(ct);
            }
        }
        return intentados;
    }
}
