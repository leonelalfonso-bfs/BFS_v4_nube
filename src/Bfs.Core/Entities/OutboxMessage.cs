namespace Bfs.Core.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Destino { get; set; } = "worms";
    public Guid PesadaId { get; set; }
    public string TicketNumero { get; set; } = "";
    public string Evento { get; set; } = "ENTRADA";
    public string PayloadJson { get; set; } = "{}";
    public string Estado { get; set; } = "pendiente";
    public int Intentos { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ProximoIntento { get; set; }
    public string? UltimoError { get; set; }
}
