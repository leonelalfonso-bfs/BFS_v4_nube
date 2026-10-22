using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bfs.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest req, BfsDbContext db, PasswordHasher<Usuario> hasher, SymmetricSecurityKey signing) =>
        {
            var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Username == req.Username && u.Activo);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new("name", user.Nombre),
                new("username", user.Username),
                new(ClaimTypes.Role, user.Rol)
            };
            if (user.EmpresaId.HasValue)
            {
                claims.Add(new Claim("empresaId", user.EmpresaId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(signing, SecurityAlgorithms.HmacSha256));

            return Results.Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                username = user.Username,
                nombre = user.Nombre,
                rol = user.Rol,
                empresaId = user.EmpresaId
            });
        });

        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            username = user.FindFirstValue("username"),
            nombre = user.FindFirstValue("name"),
            rol = user.FindFirstValue(ClaimTypes.Role)
        })).RequireAuthorization();
    }

    public static bool EsVendor(this ClaimsPrincipal user) => user.IsInRole(Roles.Vendor);
    public static bool EsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}

public record LoginRequest(string Username, string Password);
