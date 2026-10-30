using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core;
using Bfs.Infrastructure.Central;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace Bfs.Api.Nube;

/// <summary>Panel de BFS en la nube: alta de clientes, suspensión por falta de pago y "entrar como BFS".</summary>
public static class SuperadminEndpoints
{
    public static void MapSuperadminEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/superadmin").RequireAuthorization(Autorizacion.PoliticaSuperadmin);

        g.MapGet("/tenants", async (BasesNube bases) =>
        {
            await using var central = bases.Central();
            var usuarios = await central.Logins.GroupBy(l => l.TenantId).Select(x => new { x.Key, Cantidad = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Cantidad);
            var nodos = await central.Nodos.GroupBy(n => n.TenantId).Select(x => new { x.Key, Ultima = x.Max(n => n.UltimaSincronizacion) }).ToListAsync();
            var tenants = await central.Tenants.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync();
            return Results.Ok(tenants.Select(t => new
            {
                t.Id, t.Codigo, t.Nombre, t.Estado, t.MotivoEstado, t.EstadoDesde, t.CreadoEn,
                usuarios = usuarios.GetValueOrDefault(t.Id),
                ultimaSincronizacion = nodos.FirstOrDefault(n => n.Key == t.Id)?.Ultima
            }));
        });

        g.MapPost("/tenants", async (NuevoTenant dto, AltaTenants alta) =>
        {
            var (tenant, error) = await alta.CrearAsync(dto);
            return error != null ? Results.BadRequest(error) : Results.Ok(tenant);
        });

        g.MapPut("/tenants/{id:guid}/estado", async (Guid id, CambioEstado dto, BasesNube bases, IMemoryCache cache) =>
        {
            if (!EstadosTenant.Todos.Contains(dto.Estado)) return Results.BadRequest("Estado inválido: Activo, Suspendido o Baja");
            await using var central = bases.Central();
            var tenant = await central.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return Results.NotFound();
            tenant.Estado = dto.Estado;
            tenant.MotivoEstado = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
            tenant.EstadoDesde = DateTimeOffset.UtcNow;
            await central.SaveChangesAsync();
            cache.Remove($"tenant:{id}");   // que surta efecto ya en esta instancia
            return Results.Ok(tenant);
        });

        // Sesión de soporte dentro del cliente, con rol de BFS (vendor): para configurar balanzas, funciones, licencia...
        // Queda registrado con el email del superadmin. Dura 2 horas.
        g.MapPost("/tenants/{id:guid}/entrar", async (Guid id, BasesNube bases, SymmetricSecurityKey signing, ClaimsPrincipal user) =>
        {
            await using var central = bases.Central();
            var tenant = await central.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return Results.NotFound();
            if (tenant.Estado == EstadosTenant.Baja) return Results.BadRequest("El cliente está dado de baja");

            var claims = new List<Claim>(AuthEndpoints.TenantClaims(tenant))
            {
                new(JwtRegisteredClaimNames.Sub, user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? ""),
                new(Autorizacion.ClaimNombre, "BFS (soporte)"),
                new(Autorizacion.ClaimEmail, user.Email()),
                new(ClaimTypes.Role, Roles.Vendor),
                new(Autorizacion.ClaimSoporte, "true")
            };
            return Results.Ok(new
            {
                token = Tokens.Emitir(signing, claims, TimeSpan.FromHours(2)),
                email = user.Email(),
                nombre = "BFS (soporte)",
                rol = Roles.Vendor,
                tenant = tenant.Nombre,
                permisos = Permisos.DeRol(Roles.Vendor)
            });
        });
    }
}

public record CambioEstado(string Estado, string? Motivo);
