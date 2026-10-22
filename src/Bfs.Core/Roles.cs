namespace Bfs.Core;

public static class Roles
{
    /// <summary>BFS. Configura funciones, balanza, integraciones y licencia.</summary>
    public const string Vendor = "vendor";

    /// <summary>Administrador del cliente (privilegio 2 en la 3.x).</summary>
    public const string Admin = "admin";

    /// <summary>Encargado de turno (privilegio 1 en la 3.x).</summary>
    public const string Supervisor = "supervisor";

    /// <summary>Operador de balanza (privilegio 0 en la 3.x).</summary>
    public const string Operador = "operador";

    /// <summary>Cliente de la planta: solo ve sus propias pesadas (client_razon en la 3.x).</summary>
    public const string Cliente = "cliente";

    public static IReadOnlyList<string> Todos { get; } = [Vendor, Admin, Supervisor, Operador, Cliente];

    public static bool Existe(string? rol) => rol != null && Todos.Contains(rol);

    /// <summary>Equivalencia con <c>users.privilege_level</c> de la 3.x.</summary>
    public static string DesdePrivilegio3x(int privilegio) => privilegio switch
    {
        >= 2 => Admin,
        1 => Supervisor,
        _ => Operador
    };
}
