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
            var email = Usuario.NormalizarEmail(req.Email);
            var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "") == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            // Un usuario cliente sin cliente asignado no puede ver nada: mejor avisar que entrar a una pantalla vacía.
            if (user.Rol == Roles.Cliente && user.ClienteId == null)
            {
                return Results.Text("El usuario no tiene un cliente asignado", statusCode: StatusCodes.Status403Forbidden);
            }

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(Autorizacion.ClaimNombre, user.Nombre),
                new(Autorizacion.ClaimEmail, user.Email),
                new(ClaimTypes.Role, user.Rol)
            };
            if (user.EmpresaId.HasValue)
            {
                claims.Add(new Claim(Autorizacion.ClaimEmpresaId, user.EmpresaId.Value.ToString()));
            }
            if (user.ClienteId.HasValue)
            {
                claims.Add(new Claim(Autorizacion.ClaimClienteId, user.ClienteId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(signing, SecurityAlgorithms.HmacSha256));

            return Results.Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                email = user.Email,
                nombre = user.Nombre,
                rol = user.Rol,
                empresaId = user.EmpresaId,
                clienteId = user.ClienteId,
                permisos = Permisos.DeRol(user.Rol)
            });
        });

        app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            email = user.Email(),
            nombre = user.FindFirstValue(Autorizacion.ClaimNombre),
            rol = user.Rol(),
            clienteId = user.ClienteId(),
            permisos = Permisos.DeRol(user.Rol())
        })).RequireAuthorization();
    }
}

public record LoginRequest(string? Email, string? Password);
