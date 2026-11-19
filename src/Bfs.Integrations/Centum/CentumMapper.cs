using System.Globalization;
using Bfs.Core;
using Bfs.Core.Entities;

namespace Bfs.Integrations.Centum;

/// <summary>
/// Pesada → pedido para Centum. Es el mismo formato que mandaba la 3.x de INAGRO
/// (<c>ApiServer.convertToApiFormat</c>, según la planilla de Centum): mismos nombres de campo, largos y rellenos.
/// No van bruto, tara ni número de ticket (Centum no los integra).
/// </summary>
public static class CentumMapper
{
    public static Dictionary<string, object> FromPesada(Pesada p, string? codigoSociedad, DateTime entradaLocal)
    {
        var i = p.Inagro ?? new DatosInagro();
        var movimiento = i.Movimiento ?? MovimientosInagro.Compras;
        var d = new Dictionary<string, object>
        {
            ["tipo_sociedad_id"] = int.TryParse(codigoSociedad, out var soc) ? soc : (object)(codigoSociedad ?? "1"),
            ["tipo_movimiento_id"] = movimiento,
            ["numero_remito"] = Inagro.Remito(p.Comprobante),
            ["numero_analisis"] = Inagro.Digitos(i.Analisis, 6),
            ["fecha_entrada"] = entradaLocal.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["hora_entrada"] = entradaLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            // Chasis y acoplado juntos, 7 caracteres cada uno rellenos con ceros a la izquierda.
            ["patente_tractor"] = Patente7(p.PatenteChasis) + "-" + Patente7(p.PatenteAcoplado)
        };

        // Según el movimiento, el cliente del ticket es el proveedor (compras) o el cliente (ventas).
        var cuit = Inagro.Digitos(i.ClienteCuit, 11);
        var razon = p.Cliente ?? "";
        switch (movimiento)
        {
            case MovimientosInagro.Compras:
                d["proveedor"] = cuit;
                d["cuit_proveedor"] = cuit;
                d["razon_proveedor"] = razon;
                break;
            case MovimientosInagro.Ventas:
                d["cliente"] = cuit;
                d["cuit_cliente"] = cuit;
                d["razon_cliente"] = razon;
                break;
            default:
                d["cliente"] = cuit;
                break;
        }

        if (i.PesoOrigen is { } origen) d["cant_un_nivel"] = Toneladas(origen);

        var transporte = Inagro.Digitos(i.TransporteCuit, 11);
        if (transporte.Length > 0) d["transporte_cuit"] = transporte;

        d["chofer_dni"] = Inagro.Digitos(string.IsNullOrWhiteSpace(i.CuilChofer) ? p.Documento : i.CuilChofer, 11);
        d["producto_codigo"] = ProductoCodigo(i.ProductoCodigo);
        d["peso_neto_kg"] = Toneladas(p.Neto ?? 0);
        d["seccion"] = Cortar(i.Tanque, 10);
        d["observaciones"] = Cortar(p.Observaciones, 20);
        d["planta"] = (i.Planta ?? Inagro.PlantaPorDefecto).Split(':')[0].Trim();

        // En una orden de traspaso, procedencia y destino son las sucursales desde / hacia.
        if (movimiento == MovimientosInagro.Traspaso)
        {
            d["suc_desde"] = p.Procedencia ?? "";
            d["suc_hacia"] = p.Destino ?? "";
        }
        return d;
    }

    /// <summary>Kilos → toneladas "XXXXX,XX" (5 enteros con ceros, coma, 2 decimales). Negativo = "00000,00".</summary>
    public static string Toneladas(int kg)
    {
        if (kg < 0) return "00000,00";
        var t = (kg / 1000m).ToString("0.00", CultureInfo.InvariantCulture).Split('.');
        return t[0].PadLeft(5, '0') + "," + t[1];
    }

    private static string Patente7(string? patente)
    {
        var p = new string((patente ?? "").ToUpperInvariant().Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9').Take(7).ToArray());
        return p.PadLeft(7, '0');
    }

    private static string ProductoCodigo(string? codigo) =>
        new string((codigo ?? "").Where(char.IsAsciiLetterOrDigit).Take(5).ToArray()).ToUpperInvariant();

    private static string Cortar(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length > max ? t[..max] : t;
    }
}
