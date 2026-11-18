using System.Text.Json;
using System.Text.Json.Serialization;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;

namespace Bfs.Integrations.Worms;

public static class WormsEventos
{
    public const string Entrada = "ENTRADA";
    public const string Salida = "SALIDA";
}

public static class WormsOutbox
{
    /// <summary>Los campos que la balanza no registra se omiten (no van como null).</summary>
    public static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>WORMS activo y esta empresa en la lista (lista vacía = todas).</summary>
    public static bool EmpresaEnvia(IReadOnlyDictionary<string, string> configs, Guid empresaId)
    {
        if (configs.GetValueOrDefault(ConfigKeys.WormsEnabled) != "true") return false;
        var lista = configs.GetValueOrDefault(ConfigKeys.WormsEmpresas, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lista.Length == 0 || lista.Any(id => Guid.TryParse(id, out var g) && g == empresaId);
    }

    /// <summary>Encola el evento en el outbox si WORMS está activo para la empresa. Se guarda junto con la pesada.</summary>
    public static void Encolar(BfsDbContext db, IReadOnlyDictionary<string, string> configs, Pesada pesada, string evento, string empresa)
    {
        if (!EmpresaEnvia(configs, pesada.EmpresaId)) return;
        var payload = WormsMapper.FromPesada(pesada, evento, configs.GetValueOrDefault(ConfigKeys.WormsBalanza, "1"), empresa);
        var now = DateTimeOffset.UtcNow;
        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Destino = WormsDestino.Destino,
            PesadaId = pesada.Id,
            TicketNumero = pesada.TicketNumero,
            Evento = evento,
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            Estado = EstadosOutbox.Pendiente,
            CreadoEn = now,
            ProximoIntento = now
        });
    }
}
