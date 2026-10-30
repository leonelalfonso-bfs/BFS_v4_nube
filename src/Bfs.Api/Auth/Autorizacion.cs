using System.Security.Claims;
using Bfs.Core;

namespace Bfs.Api.Auth;

public static class Autorizacion
{
    public const string ClaimEmail = "email";
    public const string ClaimNombre = "name";
    public const string ClaimClienteId = "clienteId";
    public const string ClaimEmpresaId = "empresaId";

    /// <summary>Solo en la nube: a qué cliente (tenant) pertenece la sesión.</summary>
    public const string ClaimTenant = "tenant";
    public const string ClaimTenantCodigo = "tenantCodigo";
    public const string ClaimTenantNombre = "tenantNombre";

    /// <summary>Sesión de un superadmin de BFS que entró a un cliente para configurarlo (se le permite aunque esté suspendido).</summary>
    public const string ClaimSoporte = "soporte";

    public const string PoliticaSuperadmin = "superadmin";

    /// <summary>Una política por permiso, con el mismo nombre: <c>.RequireAuthorization(Permisos.X)</c>.</summary>
    public static IServiceCollection AddBfsAutorizacion(this IServiceCollection services)
    {
        services.AddAuthorization(o =>
        {
            foreach (var permiso in Permisos.Todos)
            {
                o.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.Puede(permiso)));
            }
            o.AddPolicy(PoliticaSuperadmin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Superadmin));
        });
        return services;
    }

    public static string? Rol(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role);
    public static string Email(this ClaimsPrincipal user) => user.FindFirstValue(ClaimEmail) ?? "";
    public static bool Puede(this ClaimsPrincipal user, string permiso) => Permisos.Tiene(user.Rol(), permiso);
    public static bool EsVendor(this ClaimsPrincipal user) => user.IsInRole(Roles.Vendor);
    public static bool EsSoporte(this ClaimsPrincipal user) => user.FindFirstValue(ClaimSoporte) == "true";

    public static Guid? ClienteId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimClienteId), out var id) ? id : null;
}
