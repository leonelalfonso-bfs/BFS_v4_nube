using System.Security.Claims;
using Bfs.Core;

namespace Bfs.Api.Auth;

public static class Autorizacion
{
    public const string ClaimEmail = "email";
    public const string ClaimNombre = "name";
    public const string ClaimClienteId = "clienteId";
    public const string ClaimEmpresaId = "empresaId";

    /// <summary>Una política por permiso, con el mismo nombre: <c>.RequireAuthorization(Permisos.X)</c>.</summary>
    public static IServiceCollection AddBfsAutorizacion(this IServiceCollection services)
    {
        services.AddAuthorization(o =>
        {
            foreach (var permiso in Permisos.Todos)
            {
                o.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireAssertion(ctx => ctx.User.Puede(permiso)));
            }
        });
        return services;
    }

    public static string? Rol(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Role);
    public static string Email(this ClaimsPrincipal user) => user.FindFirstValue(ClaimEmail) ?? "";
    public static bool Puede(this ClaimsPrincipal user, string permiso) => Permisos.Tiene(user.Rol(), permiso);
    public static bool EsVendor(this ClaimsPrincipal user) => user.IsInRole(Roles.Vendor);

    public static Guid? ClienteId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimClienteId), out var id) ? id : null;
}
