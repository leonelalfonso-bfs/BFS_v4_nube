using System.Net.Mail;
using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Usuarios;

public static class UsuarioEndpoints
{
    public const int LargoMinimoClave = 6;

    public static void MapUsuarioEndpoints(this RouteGroupBuilder api)
    {
        var usuarios = api.MapGroup("/usuarios").RequireAuthorization(Permisos.UsuariosGestionar);

        usuarios.MapGet("/roles", (ClaimsPrincipal user) => Results.Ok(Permisos.RolesAsignables(user.Rol())));

        usuarios.MapGet("", async (BfsDbContext db) =>
        {
            var list = await (
                from u in db.Usuarios
                join e in db.Empresas on u.EmpresaId equals e.Id into ge
                from e in ge.DefaultIfEmpty()
                join c in db.Clientes on u.ClienteId equals c.Id into gc
                from c in gc.DefaultIfEmpty()
                orderby u.Email
                select new
                {
                    u.Id,
                    u.Email,
                    u.Nombre,
                    u.Rol,
                    u.EmpresaId,
                    empresaNombre = e != null ? e.Nombre : null,
                    u.ClienteId,
                    clienteNombre = c != null ? c.RazonSocial : null,
                    u.Activo
                }).ToListAsync();
            return Results.Ok(list);
        });

        usuarios.MapPost("", async (NuevoUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            var email = Usuario.NormalizarEmail(dto.Email);
            if (!EmailValido(email)) return Results.BadRequest("Email inválido");
            if ((dto.Password ?? "").Length < LargoMinimoClave) return Results.BadRequest($"La contraseña debe tener al menos {LargoMinimoClave} caracteres");
            if (!Permisos.RolesAsignables(user.Rol()).Contains(dto.Rol)) return Results.Forbid();
            var errorCliente = await ValidarCliente(db, dto.Rol, dto.ClienteId);
            if (errorCliente != null) return Results.BadRequest(errorCliente);

            // Incluye usuarios dados de baja lógica: el email sigue ocupado.
            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email))
            {
                return Results.Conflict("Ya existe un usuario con ese email");
            }

            var entity = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = email,
                Nombre = dto.Nombre ?? "",
                Rol = dto.Rol,
                EmpresaId = dto.EmpresaId,
                ClienteId = dto.Rol == Roles.Cliente ? dto.ClienteId : null,
                Activo = true
            };
            entity.PasswordHash = hasher.HashPassword(entity, dto.Password!);
            db.Usuarios.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Email, entity.Rol });
        });

        usuarios.MapPut("/{id:guid}", async (Guid id, ActualizarUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            var entity = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
            if (entity == null) return Results.NotFound();

            var asignables = Permisos.RolesAsignables(user.Rol());
            if (!asignables.Contains(entity.Rol)) return Results.Forbid();
            var rol = string.IsNullOrWhiteSpace(dto.Rol) ? entity.Rol : dto.Rol;
            if (!asignables.Contains(rol)) return Results.Forbid();

            var clienteId = dto.ClienteId ?? entity.ClienteId;
            var errorCliente = await ValidarCliente(db, rol, clienteId);
            if (errorCliente != null) return Results.BadRequest(errorCliente);
            if (!string.IsNullOrWhiteSpace(dto.Password) && dto.Password.Length < LargoMinimoClave)
            {
                return Results.BadRequest($"La contraseña debe tener al menos {LargoMinimoClave} caracteres");
            }

            if (!string.IsNullOrWhiteSpace(dto.Nombre)) entity.Nombre = dto.Nombre;
            entity.Rol = rol;
            entity.EmpresaId = dto.EmpresaId;
            entity.ClienteId = rol == Roles.Cliente ? clienteId : null;
            if (dto.Activo.HasValue) entity.Activo = dto.Activo.Value;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                entity.PasswordHash = hasher.HashPassword(entity, dto.Password);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Email, entity.Nombre, entity.Rol, entity.EmpresaId, entity.ClienteId, entity.Activo });
        });
    }

    private static bool EmailValido(string email) =>
        MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && parsed.Host.Contains('.');

    private static async Task<string?> ValidarCliente(BfsDbContext db, string rol, Guid? clienteId)
    {
        if (rol != Roles.Cliente) return null;
        if (clienteId == null) return "Un usuario cliente necesita un cliente asignado";
        return await db.Clientes.AnyAsync(c => c.Id == clienteId) ? null : "Cliente inexistente";
    }
}

public record NuevoUsuario(string? Email, string? Password, string? Nombre, string Rol, Guid? EmpresaId, Guid? ClienteId);
public record ActualizarUsuario(string? Nombre, string? Rol, Guid? EmpresaId, Guid? ClienteId, bool? Activo, string? Password);
