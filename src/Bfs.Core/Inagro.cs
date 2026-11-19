namespace Bfs.Core;

/// <summary>Id de operación de INAGRO (tipo_movimiento_id de Centum).</summary>
public static class MovimientosInagro
{
    public const int Compras = 1;
    public const int Ventas = 2;
    public const int Traspaso = 3;

    public static IReadOnlyDictionary<int, string> Nombres { get; } = new Dictionary<int, string>
    {
        [Compras] = "Compras",
        [Ventas] = "Ventas",
        [Traspaso] = "OT (orden de traspaso)"
    };

    /// <summary>Como lo imprimía el ticket de la 3.x.</summary>
    public static string? Ticket(int? m) => m switch
    {
        Compras => "Compra (1)",
        Ventas => "Venta (2)",
        Traspaso => "Traspaso (3)",
        _ => null
    };

    /// <summary>El peso de origen solo se carga en compras y órdenes de traspaso.</summary>
    public static bool LlevaPesoOrigen(int? m) => m is Compras or Traspaso;

    /// <summary>INGRESO / EGRESO de la 4.0 según el movimiento: una venta sale; compras y traspasos entran.</summary>
    public static string Tipo(int? m) => m == Ventas ? "EGRESO" : "INGRESO";
}

/// <summary>Reglas y formatos de los datos de INAGRO.</summary>
public static class Inagro
{
    /// <summary>Plantas de INAGRO. Se elige una por instalación (Configuración → General).</summary>
    public static IReadOnlyList<string> Plantas { get; } = ["1: Alvear", "2: Campana"];

    public const string PlantaPorDefecto = "1: Alvear";

    /// <summary>Sociedad de Centum que no se envía (en la 3.x: "4: No Vincula Centum").</summary>
    public const string SociedadNoVincula = "4";

    /// <summary>Empresas con las que arranca una instalación nueva: las sociedades de Centum de la 3.x.</summary>
    public static IReadOnlyList<(string Nombre, string Codigo)> SociedadesIniciales { get; } =
    [
        ("Inagro S.A.", "1"),
        ("Inagro S.R.L.", "2"),
        ("Otros", "3"),
        ("No Vincula Centum", SociedadNoVincula)
    ];

    /// <summary>Se envía a Centum solo si la empresa tiene código de sociedad y no es "No vincula".</summary>
    public static bool SociedadEnvia(string? codigo) =>
        !string.IsNullOrWhiteSpace(codigo) && codigo.Trim() != SociedadNoVincula;

    /// <summary>Solo los dígitos, cortando al largo máximo.</summary>
    public static string Digitos(string? s, int max) =>
        new string((s ?? "").Where(char.IsDigit).Take(max).ToArray());

    /// <summary>
    /// Remito "PPPPP-NNNNNNNN" (punto de venta de 5 y número de 8), como lo formatea la 3.x.
    /// Con guion se rellena cada parte; sin guion se toman 13 dígitos. Vacío = "00000-00000000".
    /// </summary>
    public static string Remito(string? remito)
    {
        if (string.IsNullOrWhiteSpace(remito)) return "00000-00000000";
        var partes = remito.Split('-');
        if (partes.Length >= 2)
        {
            var pv = Ultimos(Digitos(partes[0], int.MaxValue), 5).PadLeft(5, '0');
            var nro = Ultimos(Digitos(partes[1], int.MaxValue), 8).PadLeft(8, '0');
            return $"{pv}-{nro}";
        }
        var todo = Digitos(remito, 13).PadLeft(13, '0');
        return $"{todo[..5]}-{todo[5..]}";
    }

    private static string Ultimos(string s, int n) => s.Length > n ? s[^n..] : s;
}
