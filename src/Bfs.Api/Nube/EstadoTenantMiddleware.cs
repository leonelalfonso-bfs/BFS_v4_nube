using Bfs.Api.Auth;
using Bfs.Infrastructure.Central;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Bfs.Api.Nube;

/// <summary>
/// En cada pedido de un cliente suspendido o dado de baja se corta con 403, aunque la sesión se haya abierto antes.
/// El estado se cachea 30 segundos: suspender tarda como mucho eso en hacer efecto.
/// Las sesiones de soporte de BFS pasan igual (para poder revisar o exportar los datos de un cliente suspendido).
/// </summary>
public sealed class EstadoTenantMiddleware(RequestDelegate next)
{
    public static readonly TimeSpan Cache = TimeSpan.FromSeconds(30);

    public async Task InvokeAsync(HttpContext ctx, BasesNube bases, IMemoryCache cache)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated == true && !user.EsSoporte() && Guid.TryParse(user.FindFirst(Autorizacion.ClaimTenant)?.Value, out var id))
        {
            var tenant = await cache.GetOrCreateAsync($"tenant:{id}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = Cache;
                await using var central = bases.Central();
                return await central.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            });
            if (tenant == null || tenant.Estado != EstadosTenant.Activo)
            {
                await Bloqueado(tenant).ExecuteAsync(ctx);
                return;
            }
        }

        try
        {
            await next(ctx);
        }
        catch (SinTenantException ex) when (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsync(ex.Message);
        }
    }

    public static IResult Bloqueado(Tenant? t) => Results.Text(
        t?.Estado == EstadosTenant.Suspendido
            ? $"La cuenta de {t.Nombre} está suspendida. Comunicate con Balanzas Full Service."
            : "La cuenta no está activa. Comunicate con Balanzas Full Service.",
        statusCode: StatusCodes.Status403Forbidden);
}
