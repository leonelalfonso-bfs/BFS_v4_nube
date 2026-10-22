namespace Bfs.Core.Entities;

public class Pesada
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public string TicketNumero { get; set; } = "";
    public string Tipo { get; set; } = "INGRESO";
    public string Estado { get; set; } = "ENTRADA";
    public string PatenteChasis { get; set; } = "";
    public string? PatenteAcoplado { get; set; }
    public string? Conductor { get; set; }
    public string? Documento { get; set; }
    public string? Cliente { get; set; }
    public string? Transporte { get; set; }
    public string? Producto { get; set; }
    public string? Procedencia { get; set; }
    public string? Destino { get; set; }
    public string? Comprobante { get; set; }
    public string? Observaciones { get; set; }
    public string Operador { get; set; } = "";
    public string? Balanza { get; set; }
    public int PesoEntrada { get; set; }
    public int? PesoSalida { get; set; }
    public int? Bruto { get; set; }
    public int? Tara { get; set; }
    public int TaraPallets { get; set; }
    public int? Neto { get; set; }
    public string DatosExtraJson { get; set; } = "{}";
    public DateTimeOffset EntradaEn { get; set; }
    public DateTimeOffset? SalidaEn { get; set; }
}
