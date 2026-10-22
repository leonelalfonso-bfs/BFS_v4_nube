using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;

namespace Bfs.Integrations.Worms;

public static class WormsOutbox
{
    /// <summary>Encola el evento en el outbox si WORMS está activo. Se guarda junto con la pesada.</summary>
    public static void Encolar(BfsDbContext db, IReadOnlyDictionary<string, string> configs, Pesada pesada, string evento)
    {
        if (configs.GetValueOrDefault(ConfigKeys.WormsEnabled) != "true") return;
        var payload = WormsMapper.FromPesada(pesada, evento, configs.GetValueOrDefault(ConfigKeys.WormsBalanza, "1"));
        var now = DateTimeOffset.UtcNow;
        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Destino = "worms",
            PesadaId = pesada.Id,
            TicketNumero = pesada.TicketNumero,
            Evento = evento,
            PayloadJson = JsonSerializer.Serialize(payload),
            Estado = "pendiente",
            CreadoEn = now,
            ProximoIntento = now
        });
    }
}
