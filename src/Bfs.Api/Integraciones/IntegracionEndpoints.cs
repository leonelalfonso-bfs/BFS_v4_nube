using Bfs.Core;
using Bfs.Infrastructure.Data;
using Bfs.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Integraciones;

/// <summary>Configuración → API: qué se mandó a cada integración, qué está esperando y qué fue rechazado.</summary>
public static class IntegracionEndpoints
{
    public const int Ultimos = 50;

    public static void MapIntegracionEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/integraciones").RequireAuthorization(Permisos.ConfigBfs);

        g.MapGet("", async (string? estado, BfsDbContext db, DespachadorOutbox despachador) =>
            Results.Ok(await EstadoAsync(db, despachador, estado)));

        // Manda ya lo pendiente, sin esperar la próxima vuelta (ni la espera de los reintentos).
        g.MapPost("/enviar", async (BfsDbContext db, DespachadorOutbox despachador) =>
        {
            var ahora = DateTimeOffset.UtcNow;
            await db.Outbox.Where(o => o.Estado == EstadosOutbox.Pendiente)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.ProximoIntento, ahora));
            await despachador.ProcesarAsync(db);
            return Results.Ok(await EstadoAsync(db, despachador, null));
        });

        g.MapPost("/{id:guid}/reintentar", async (Guid id, BfsDbContext db) =>
        {
            var m = await db.Outbox.FindAsync(id);
            if (m == null) return Results.NotFound();
            if (m.Estado == EstadosOutbox.Enviado) return Results.BadRequest("Ya fue enviado");
            Reencolar(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{id:guid}/descartar", async (Guid id, BfsDbContext db) =>
        {
            var m = await db.Outbox.FindAsync(id);
            if (m == null) return Results.NotFound();
            if (m.Estado == EstadosOutbox.Enviado) return Results.BadRequest("Ya fue enviado");
            m.Estado = EstadosOutbox.Descartado;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/{destino}/reintentar-errores", async (string destino, BfsDbContext db) =>
        {
            var errores = await db.Outbox.Where(o => o.Destino == destino && o.Estado == EstadosOutbox.Error).ToListAsync();
            errores.ForEach(Reencolar);
            await db.SaveChangesAsync();
            return Results.Ok(new { reintentados = errores.Count });
        });
    }

    /// <summary>Vuelve a la cola desde cero: la espera entre reintentos arranca otra vez en 1 minuto.</summary>
    private static void Reencolar(Core.Entities.OutboxMessage m)
    {
        m.Estado = EstadosOutbox.Pendiente;
        m.Intentos = 0;
        m.ProximoIntento = DateTimeOffset.UtcNow;
    }

    private static async Task<EstadoIntegraciones> EstadoAsync(BfsDbContext db, DespachadorOutbox despachador, string? estado)
    {
        var config = await db.Configuraciones.AsNoTracking().Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor);
        var conteos = await db.Outbox.GroupBy(o => new { o.Destino, o.Estado })
            .Select(x => new { x.Key.Destino, x.Key.Estado, Cantidad = x.Count() })
            .ToListAsync();
        int Contar(string destino, string e) => conteos.Where(c => c.Destino == destino && c.Estado == e).Sum(c => c.Cantidad);

        var destinos = despachador.Destinos.Select(d => new DestinoDto(
            d.Nombre, d.Habilitado(config),
            Contar(d.Nombre, EstadosOutbox.Pendiente), Contar(d.Nombre, EstadosOutbox.Error),
            Contar(d.Nombre, EstadosOutbox.Enviado), Contar(d.Nombre, EstadosOutbox.Descartado))).ToList();

        var q = db.Outbox.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(estado)) q = q.Where(o => o.Estado == estado);
        var mensajes = await q.OrderByDescending(o => o.CreadoEn).Take(Ultimos)
            .Select(o => new MensajeDto(o.Id, o.Destino, o.TicketNumero, o.Evento, o.Estado, o.Intentos, o.CreadoEn, o.ProximoIntento, o.UltimoError))
            .ToListAsync();
        return new EstadoIntegraciones(destinos, mensajes);
    }
}

public record DestinoDto(string Nombre, bool Habilitado, int Pendientes, int Errores, int Enviados, int Descartados);

public record MensajeDto(Guid Id, string Destino, string Ticket, string Evento, string Estado, int Intentos,
    DateTimeOffset CreadoEn, DateTimeOffset ProximoIntento, string? UltimoError);

public record EstadoIntegraciones(List<DestinoDto> Destinos, List<MensajeDto> Mensajes);
