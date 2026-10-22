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
    public static void MapUsuarioEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/usuarios", async (BfsDbContext db, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor() && !user.EsAdmin()) return Results.Forbid();
            var list = await (
                from u in db.Usuarios
                join e in db.Empresas on u.EmpresaId equals e.Id into gj
                from e in gj.DefaultIfEmpty()
                orderby u.Username
                select new
                {
                    u.Id,
                    u.Username,
                    u.Nombre,
                    u.Rol,
                    u.EmpresaId,
                    empresaNombre = e != null ? e.Nombre : null,
                    u.Activo
                }).ToListAsync();
            return Results.Ok(list);
        });

        api.MapPost("/usuarios", async (NuevoUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor() && !user.EsAdmin()) return Results.Forbid();
            if (dto.Rol == Roles.Vendor && !user.EsVendor()) return Results.Forbid();

            var entity = new Usuario
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                Nombre = dto.Nombre,
                Rol = dto.Rol,
                EmpresaId = dto.EmpresaId,
                Activo = true
            };
            entity.PasswordHash = hasher.HashPassword(entity, dto.Password);
            db.Usuarios.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Username, entity.Rol });
        });

        api.MapPut("/usuarios/{id:guid}", async (Guid id, ActualizarUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor() && !user.EsAdmin()) return Results.Forbid();
            var entity = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
            if (entity == null) return Results.NotFound();
            if ((entity.Rol == Roles.Vendor || dto.Rol == Roles.Vendor) && !user.EsVendor()) return Results.Forbid();

            if (!string.IsNullOrWhiteSpace(dto.Nombre)) entity.Nombre = dto.Nombre;
            if (!string.IsNullOrWhiteSpace(dto.Rol)) entity.Rol = dto.Rol;
            entity.EmpresaId = dto.EmpresaId;
            if (dto.Activo.HasValue) entity.Activo = dto.Activo.Value;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                entity.PasswordHash = hasher.HashPassword(entity, dto.Password);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Username, entity.Nombre, entity.Rol, entity.EmpresaId, entity.Activo });
        });
    }
}

public record NuevoUsuario(string Username, string Password, string Nombre, string Rol, Guid? EmpresaId);
public record ActualizarUsuario(string? Nombre, string? Rol, Guid? EmpresaId, bool? Activo, string? Password);
