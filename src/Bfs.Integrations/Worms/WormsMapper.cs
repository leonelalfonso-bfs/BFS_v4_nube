using Bfs.Core.Entities;

namespace Bfs.Integrations.Worms;

public static class WormsMapper
{
    public static WormsPayload FromPesada(Pesada pesada, string evento, string balanza, string? empresa = null)
    {
        return new WormsPayload
        {
            TicketNro = pesada.TicketNumero,
            Empresa = EmptyToNull(empresa),
            Evento = evento,
            Tipo = pesada.Tipo,
            Balanza = balanza,
            Operador = pesada.Operador,
            EntradaEn = pesada.EntradaEn.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz"),
            SalidaEn = evento == WormsEventos.Salida ? pesada.SalidaEn?.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:sszzz") : null,
            PesoEntrada = pesada.PesoEntrada,
            PesoSalida = evento == WormsEventos.Salida ? pesada.PesoSalida : null,
            Producto = EmptyToNull(pesada.Producto),
            Cliente = EmptyToNull(pesada.Cliente),
            Transporte = EmptyToNull(pesada.Transporte),
            // WORMS tiene un solo campo: procedencia en los ingresos, destino en los egresos.
            Procedencia = EmptyToNull(pesada.Tipo == "EGRESO" ? pesada.Destino ?? pesada.Procedencia : pesada.Procedencia ?? pesada.Destino),
            Comprobante = EmptyToNull(pesada.Comprobante),
            Conductor = EmptyToNull(pesada.Conductor),
            Documento = EmptyToNull(pesada.Documento),
            PatenteChasis = pesada.PatenteChasis.Replace(" ", "", StringComparison.Ordinal),
            PatenteAcoplado = EmptyToNull(pesada.PatenteAcoplado?.Replace(" ", "", StringComparison.Ordinal)),
            ContenedorNro = EmptyToNull(pesada.Aduana?.ContenedorId),
            TaraContenedor = pesada.Aduana is { ContenedorId: not null, ContenedorTara: > 0 } a ? a.ContenedorTara : null,
            Observaciones = EmptyToNull(pesada.Observaciones)
        };
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
