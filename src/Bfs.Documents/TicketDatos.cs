using Bfs.Core.Entities;

namespace Bfs.Documents;

/// <summary>Datos de la planta que salen en el encabezado del ticket.</summary>
public record EmpresaTicket(string RazonSocial, string? Cuit, string? Direccion, string? Localidad, string? Provincia, string? Telefono, string? Email);

/// <summary>Configuración de aduana de la planta (Planta → Aduana).</summary>
public record AduanaTicket(string? Codigo, string? Lot, string? Certificado, string? CertificadoVence, string? Habilitacion = null);

/// <summary>Una foto de cámara para el ticket: momento (entrada / salida), nombre de la cámara y la imagen.</summary>
public record FotoTicket(string Momento, string Camara, byte[] Imagen);

/// <summary>
/// Todo lo necesario para imprimir un ticket, ya resuelto (CUIT del cliente, código de producto, fechas locales).
/// Los generadores no tocan la base: así se pueden probar solos.
/// </summary>
public record TicketDatos(
    Pesada Pesada,
    EmpresaTicket Empresa,
    string Titulo,
    string? Pie,
    byte[]? Logo,
    DateTime EntradaLocal,
    DateTime? SalidaLocal,
    string? ClienteCuit,
    string? TransporteCuit,
    string? ProductoCodigo,
    AduanaTicket? Aduana)
{
    /// <summary>Fotos de las cámaras (automatización): si hay, la mitad de abajo de la hoja las muestra en vez del duplicado.</summary>
    public IReadOnlyList<FotoTicket> Fotos { get; init; } = [];

    /// <summary>Configuración "marcar el exceso por eje en el ticket"; si no, el ticket muestra los pesos sin comparar.</summary>
    public bool MarcarExcesoEjes { get; init; }

    /// <summary>CUIT del exportador y del ATA resueltos desde el catálogo si en la pesada habían quedado vacíos.</summary>
    public string? ExportadorCuit { get; init; }
    public string? AtaCuit { get; init; }
    public string? AtaCodigo { get; init; }
    public string ComprobanteLabel { get; init; } = "Remito";

    public Bfs.Core.PartesPesada Partes => Bfs.Core.PartesPesada.Leer(Pesada.PartesJson);
    public bool EsControlEjes => Pesada.Modo == Bfs.Core.ModosPesaje.ControlEjes;

    public bool EsTaraConocida => Pesada.Modo == Bfs.Core.ModosPesaje.TaraConocida;
    public bool EsAduana => Pesada.TipoOperacion == Bfs.Core.TiposOperacion.Aduana && Pesada.Aduana != null;
    public bool EsAnulada => Pesada.Estado == Bfs.Core.EstadosPesada.Anulada;
    public bool EsPendiente => Pesada.Estado == Bfs.Core.EstadosPesada.Entrada;
}

public static class Formato
{
    /// <summary>"17020 kg", sin separador de miles, como el ticket de la 3.x.</summary>
    public static string Kg(int? kg) => kg.HasValue ? $"{kg.Value} kg" : "-";
    public static string Fecha(DateTime? d) => d?.ToString("dd/MM/yyyy HH:mm:ss") ?? "";
    public static string Texto(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s;
}
