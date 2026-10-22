namespace Bfs.Core.Entities;

public class Pesada : EntidadSync
{
    public Guid EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }
    public string TicketNumero { get; set; } = "";
    public string Tipo { get; set; } = "INGRESO";
    public string Estado { get; set; } = EstadosPesada.Entrada;
    public string Modo { get; set; } = ModosPesaje.Normal;
    public string TipoOperacion { get; set; } = TiposOperacion.Normal;
    public DatosAduana? Aduana { get; set; }
    public string PatenteChasis { get; set; } = "";
    public string? PatenteAcoplado { get; set; }
    public string? Conductor { get; set; }
    public string? Documento { get; set; }
    public string? Cliente { get; set; }

    /// <summary>Cliente del catálogo, si el texto coincide con uno. Es lo que filtra el portal del cliente.</summary>
    public Guid? ClienteId { get; set; }
    public string? Transporte { get; set; }
    public string? Producto { get; set; }
    public string? Procedencia { get; set; }
    public string? Destino { get; set; }
    public string? Comprobante { get; set; }
    public string? Observaciones { get; set; }
    public string Operador { get; set; } = "";
    public string? OperadorSalida { get; set; }
    public string? Balanza { get; set; }
    public string? BalanzaSalida { get; set; }

    /// <summary>Quién cargó el peso a mano (null = vino de la balanza).</summary>
    public string? EntradaManualPor { get; set; }
    public string? SalidaManualPor { get; set; }
    public int PesoEntrada { get; set; }
    public int? PesoSalida { get; set; }
    public int? Bruto { get; set; }
    public int? Tara { get; set; }
    public int TaraPallets { get; set; }
    public int? Neto { get; set; }
    public string DatosExtraJson { get; set; } = "{}";
    public DateTimeOffset EntradaEn { get; set; }
    public DateTimeOffset? SalidaEn { get; set; }

    public DateTimeOffset? AnuladaEn { get; set; }
    public string? AnuladaPor { get; set; }
    public string? MotivoAnulacion { get; set; }
}
