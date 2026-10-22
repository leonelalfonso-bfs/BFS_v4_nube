namespace Bfs.Core;

/// <summary>
/// Acciones que se habilitan por rol. Cada una es también una política de autorización de la API
/// y se manda al frontend para mostrar u ocultar pantallas.
/// </summary>
public static class Permisos
{
    public const string PesajeOperar = "pesaje.operar";
    public const string PesajePesoManual = "pesaje.peso_manual";
    public const string PesajeAnular = "pesaje.anular";
    public const string PesajeReimprimir = "pesaje.reimprimir";
    public const string PesadasVer = "pesadas.ver";
    public const string PesadasVerPropias = "pesadas.ver_propias";
    public const string CatalogosVer = "catalogos.ver";
    public const string CatalogosEditar = "catalogos.editar";
    public const string UsuariosGestionar = "usuarios.gestionar";
    public const string EmpresasGestionar = "empresas.gestionar";
    public const string ConfigBfs = "config.bfs";

    /// <summary>Datos de la planta que carga el cliente: empresas, aduana, clave de ingreso manual.</summary>
    public const string ConfigPlanta = "config.planta";
    public const string LicenciaGestionar = "licencia.gestionar";

    public static IReadOnlyList<string> Todos { get; } =
    [
        PesajeOperar, PesajePesoManual, PesajeAnular, PesajeReimprimir,
        PesadasVer, PesadasVerPropias,
        CatalogosVer, CatalogosEditar,
        UsuariosGestionar, EmpresasGestionar, ConfigBfs, ConfigPlanta, LicenciaGestionar
    ];

    private static readonly string[] Operador = [PesajeOperar, PesajeReimprimir, PesadasVer, CatalogosVer];
    private static readonly string[] Supervisor = [.. Operador, PesajePesoManual, PesajeAnular, CatalogosEditar];
    private static readonly string[] Admin = [.. Supervisor, UsuariosGestionar, ConfigPlanta];

    private static readonly IReadOnlyDictionary<string, string[]> PorRol = new Dictionary<string, string[]>
    {
        [Roles.Vendor] = Todos.Except([PesadasVerPropias]).ToArray(),
        [Roles.Admin] = Admin,
        [Roles.Supervisor] = Supervisor,
        [Roles.Operador] = Operador,
        [Roles.Cliente] = [PesadasVerPropias]
    };

    public static IReadOnlyList<string> DeRol(string? rol) =>
        rol != null && PorRol.TryGetValue(rol, out var permisos) ? permisos : [];

    public static bool Tiene(string? rol, string permiso) => DeRol(rol).Contains(permiso);

    /// <summary>Roles que un usuario con este rol puede crear o asignar.</summary>
    public static IReadOnlyList<string> RolesAsignables(string? rol) => rol switch
    {
        Roles.Vendor => Roles.Todos,
        Roles.Admin => [Roles.Admin, Roles.Supervisor, Roles.Operador, Roles.Cliente],
        _ => []
    };
}
