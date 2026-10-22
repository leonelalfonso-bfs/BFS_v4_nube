using Bfs.Core.Entities;

namespace Bfs.Integrations.Worms;

public static class WormsMapper
{
    public static WormsPayload FromPesada(Pesada pesada, string evento, string balanza)
    {
        return new WormsPayload
        {
            TicketNro = pesada.TicketNumero,
            Evento = evento,
            Tipo = pesada.Tipo,
            Balanza = balanza,
            Operador = pesada.Operador,
            EntradaEn = pesada.EntradaEn.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
            SalidaEn = evento == "SALIDA" ? pesada.SalidaEn?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz") : null,
            PesoEntrada = pesada.PesoEntrada,
            PesoSalida = evento == "SALIDA" ? pesada.PesoSalida : null,
            Producto = EmptyToNull(pesada.Producto),
            Cliente = EmptyToNull(pesada.Cliente),
            Transporte = EmptyToNull(pesada.Transporte),
            Procedencia = EmptyToNull(string.IsNullOrWhiteSpace(pesada.Destino) ? pesada.Procedencia : pesada.Procedencia),
            Comprobante = EmptyToNull(pesada.Comprobante),
            Conductor = EmptyToNull(pesada.Conductor),
            Documento = EmptyToNull(pesada.Documento),
            PatenteChasis = pesada.PatenteChasis.Replace(" ", "", StringComparison.Ordinal),
            PatenteAcoplado = EmptyToNull(pesada.PatenteAcoplado?.Replace(" ", "", StringComparison.Ordinal)),
            Observaciones = EmptyToNull(pesada.Observaciones)
        };
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
