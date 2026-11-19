namespace Bfs.Core.Entities;

/// <summary>
/// Datos de INAGRO de una pesada (edición Inagro). Son los campos que su 3.x agregó para Centum y para el remito
/// preimpreso. Los datos del cliente y del transporte se guardan como estaban al pesar: el remito y Centum
/// tienen que salir igual aunque después se cambie el catálogo.
/// </summary>
public class DatosInagro
{
    /// <summary>"Id de operación" de Centum: 1 compras, 2 ventas, 3 orden de traspaso (<see cref="MovimientosInagro"/>).</summary>
    public int? Movimiento { get; set; }

    /// <summary>Kilos declarados en origen (solo compras y OT). Centum lo recibe en toneladas.</summary>
    public int? PesoOrigen { get; set; }

    /// <summary>Tanque o sección del catálogo de tanques. Centum: "seccion".</summary>
    public string? Tanque { get; set; }

    /// <summary>Número de análisis de laboratorio (hasta 6 dígitos).</summary>
    public string? Analisis { get; set; }
    public string? Precintos { get; set; }

    /// <summary>Código del producto al momento de pesar (Centum lo exige).</summary>
    public string? ProductoCodigo { get; set; }

    /// <summary>CUIL del chofer (11 dígitos). Centum: "chofer_dni".</summary>
    public string? CuilChofer { get; set; }

    /// <summary>Texto del recuadro del remito preimpreso.</summary>
    public string? ObservacionesRemito { get; set; }

    /// <summary>Planta de la instalación al pesar ("1: Alvear", "2: Campana"). Centum: "planta".</summary>
    public string? Planta { get; set; }

    public string? ClienteCuit { get; set; }
    public string? ClienteDireccion { get; set; }
    public string? ClienteLocalidad { get; set; }
    public string? ClienteCondicionIva { get; set; }
    public string? TransporteCuit { get; set; }
    public string? TransporteDireccion { get; set; }
    public string? TransporteLocalidad { get; set; }
    public string? TransporteCondicionIva { get; set; }
}
