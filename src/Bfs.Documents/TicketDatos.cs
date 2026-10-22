using Bfs.Core.Entities;

namespace Bfs.Documents;

/// <summary>Datos de la planta que salen en el encabezado del ticket.</summary>
public record EmpresaTicket(string RazonSocial, string? Cuit, string? Direccion, string? Localidad, string? Provincia, string? Telefono, string? Email);

/// <summary>Configuración de aduana de la planta (Planta → Aduana).</summary>
public record AduanaTicket(string? Codigo, string? Lot, string? Certificado, string? CertificadoVence);

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
