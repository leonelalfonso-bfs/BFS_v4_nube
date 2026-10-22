using System.Text.Json.Serialization;

namespace Bfs.Integrations.Worms;

public class WormsPayload
{
    [JsonPropertyName("ticket_nro")] public string TicketNro { get; set; } = "";
    [JsonPropertyName("evento")] public string Evento { get; set; } = "";
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "";
    [JsonPropertyName("balanza")] public string Balanza { get; set; } = "";
    [JsonPropertyName("operador")] public string Operador { get; set; } = "";
    [JsonPropertyName("entrada_en")] public string EntradaEn { get; set; } = "";
    [JsonPropertyName("salida_en")] public string? SalidaEn { get; set; }
    [JsonPropertyName("peso_entrada")] public int PesoEntrada { get; set; }
    [JsonPropertyName("peso_salida")] public int? PesoSalida { get; set; }
    [JsonPropertyName("producto")] public string? Producto { get; set; }
    [JsonPropertyName("cliente")] public string? Cliente { get; set; }
    [JsonPropertyName("transporte")] public string? Transporte { get; set; }
    [JsonPropertyName("procedencia")] public string? Procedencia { get; set; }
    [JsonPropertyName("comprobante")] public string? Comprobante { get; set; }
    [JsonPropertyName("conductor")] public string? Conductor { get; set; }
    [JsonPropertyName("documento")] public string? Documento { get; set; }
    [JsonPropertyName("patente_chasis")] public string PatenteChasis { get; set; } = "";
    [JsonPropertyName("patente_acoplado")] public string? PatenteAcoplado { get; set; }
    [JsonPropertyName("observaciones")] public string? Observaciones { get; set; }
}
