using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bfs.Api.Nube;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bfs.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app, ModoDespliegue modo)
    {
        if (modo == ModoDespliegue.Nodo)
        {
            app.MapPost("/api/auth/login", async (LoginRequest req, BfsDbContext db, PasswordHasher<Usuario> hasher, SymmetricSecurityKey signing) =>
                await LoginEnBase(req, db, hasher, signing, []) ?? Results.Unauthorized());
        }
        else
        {
            app.MapPost("/api/auth/login", LoginNube);
        }

        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            email = user.Email(),
            nombre = user.FindFirstValue(Autorizacion.ClaimNombre),
            rol = user.Rol(),
            clienteId = user.ClienteId(),
            tenant = user.FindFirstValue(Autorizacion.ClaimTenantNombre),
            soporte = user.EsSoporte(),
            permisos = Permisos.DeRol(user.Rol())
        })).RequireAuthorization();
    }

    /// <summary>
    /// En la nube hay un solo dominio para todos: primero se mira si es un superadmin de BFS;
    /// si no, el índice central dice de qué cliente es el email y se valida contra la base de ese cliente.
    /// </summary>
    private static async Task<IResult> LoginNube(LoginRequest req, BasesNube bases, PasswordHasher<Usuario> hasher, SymmetricSecurityKey signing)
    {
        var email = Usuario.NormalizarEmail(req.Email);
        await using var central = bases.Central();

        var sa = await central.Superadmins.AsNoTracking().FirstOrDefaultAsync(s => s.Email == email && s.Activo);
        if (sa != null)
        {
            if (hasher.VerifyHashedPassword(new Usuario(), sa.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, sa.Id.ToString()),
                new(Autorizacion.ClaimNombre, "BFS"),
                new(Autorizacion.ClaimEmail, sa.Email),
                new(ClaimTypes.Role, Roles.Superadmin)
            };
            return Results.Ok(new
            {
                token = Tokens.Emitir(signing, claims, TimeSpan.FromHours(8)),
                email = sa.Email,
                nombre = "BFS",
                rol = Roles.Superadmin,
                permisos = Permisos.DeRol(Roles.Superadmin)
            });
        }

        var login = await central.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.Email == email);
        if (login == null) return Results.Unauthorized();
        var tenant = await central.Tenants.AsNoTracking().FirstAsync(t => t.Id == login.TenantId);
        if (tenant.Estado != EstadosTenant.Activo) return EstadoTenantMiddleware.Bloqueado(tenant);

        await using var db = bases.Tenant(tenant.Codigo);
        return await LoginEnBase(req, db, hasher, signing, TenantClaims(tenant)) ?? Results.Unauthorized();
    }

    public static Claim[] TenantClaims(Tenant t) =>
    [
        new(Autorizacion.ClaimTenant, t.Id.ToString()),
        new(Autorizacion.ClaimTenantCodigo, t.Codigo),
        new(Autorizacion.ClaimTenantNombre, t.Nombre)
    ];

    /// <returns>null si el email o la clave no coinciden.</returns>
    private static async Task<IResult?> LoginEnBase(LoginRequest req, BfsDbContext db, PasswordHasher<Usuario> hasher, SymmetricSecurityKey signing,
        IEnumerable<Claim> extra)
    {
        var email = Usuario.NormalizarEmail(req.Email);
        var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo);
        if (user == null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
        {
            return null;
        }

        // Un usuario cliente sin cliente asignado no puede ver nada: mejor avisar que entrar a una pantalla vacía.
        if (user.Rol == Roles.Cliente && user.ClienteId == null)
        {
            return Results.Text("El usuario no tiene un cliente asignado", statusCode: StatusCodes.Status403Forbidden);
        }

        var claims = new List<Claim>(extra)
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(Autorizacion.ClaimNombre, user.Nombre),
            new(Autorizacion.ClaimEmail, user.Email),
            new(ClaimTypes.Role, user.Rol)
        };
        if (user.EmpresaId.HasValue) claims.Add(new Claim(Autorizacion.ClaimEmpresaId, user.EmpresaId.Value.ToString()));
        if (user.ClienteId.HasValue) claims.Add(new Claim(Autorizacion.ClaimClienteId, user.ClienteId.Value.ToString()));

        return Results.Ok(new
        {
            token = Tokens.Emitir(signing, claims, TimeSpan.FromHours(12)),
            email = user.Email,
            nombre = user.Nombre,
            rol = user.Rol,
            empresaId = user.EmpresaId,
            clienteId = user.ClienteId,
            permisos = Permisos.DeRol(user.Rol)
        });
    }
}

public static class Tokens
{
    public static string Emitir(SymmetricSecurityKey signing, IEnumerable<Claim> claims, TimeSpan duracion) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(duracion),
            signingCredentials: new SigningCredentials(signing, SecurityAlgorithms.HmacSha256)));
}

public record LoginRequest(string? Email, string? Password);
