using System.Security.Claims;
using Bfs.Api.Auth;

namespace Bfs.Api.Nube;

/// <summary>El cliente de la sesión actual (en el nodo siempre es null: hay una sola base).</summary>
public sealed class TenantActual(IHttpContextAccessor http)
{
    private ClaimsPrincipal? User => http.HttpContext?.User;

    public Guid? Id => Guid.TryParse(User?.FindFirstValue(Autorizacion.ClaimTenant), out var id) ? id : null;
    public string? Codigo => User?.FindFirstValue(Autorizacion.ClaimTenantCodigo);

    /// <summary>Carpeta del logo: en la nube una por cliente (si no, todos compartirían el mismo logo).</summary>
    public string CarpetaBranding(Core.BfsPaths paths)
    {
        if (Codigo == null) return paths.Branding;
        var dir = Path.Combine(paths.Branding, Codigo);
        Directory.CreateDirectory(dir);
        return dir;
    }
}

/// <summary>Se pidió la base del cliente en una sesión que no tiene cliente (un superadmin sin "entrar").</summary>
public sealed class SinTenantException() : InvalidOperationException("Esta sesión no pertenece a ningún cliente");
