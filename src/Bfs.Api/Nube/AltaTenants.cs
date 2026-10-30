using System.Net.Mail;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Nube;

public record NuevoTenant(string? Nombre, string? Codigo, string? AdminEmail, string? AdminNombre, string? AdminPassword);

/// <summary>Alta de un cliente: su base (con todas las migraciones), su empresa y su usuario administrador.</summary>
public sealed class AltaTenants(BasesNube bases, PasswordHasher<Usuario> hasher, ILogger<AltaTenants> log)
{
    public const int LargoMinimoClave = 8;

    public async Task<(Tenant? Tenant, string? Error)> CrearAsync(NuevoTenant dto)
    {
        var nombre = dto.Nombre?.Trim();
        var codigo = dto.Codigo?.Trim().ToLowerInvariant();
        var email = Usuario.NormalizarEmail(dto.AdminEmail);
        if (string.IsNullOrWhiteSpace(nombre)) return (null, "Falta el nombre del cliente");
        if (!BasesNube.CodigoValido(codigo)) return (null, "El código va en minúsculas, números o _ (de 2 a 40), por ejemplo acopio_sur");
        if (!MailAddress.TryCreate(email, out var mail) || mail.Address != email || !mail.Host.Contains('.')) return (null, "Email del administrador inválido");
        if ((dto.AdminPassword ?? "").Length < LargoMinimoClave) return (null, $"La clave del administrador tiene que tener al menos {LargoMinimoClave} caracteres");

        await using var central = bases.Central();
        if (await central.Tenants.AnyAsync(t => t.Codigo == codigo)) return (null, $"Ya existe un cliente con el código {codigo}");
        if (await central.Logins.AnyAsync(l => l.Email == email) || await central.Superadmins.AnyAsync(s => s.Email == email))
        {
            return (null, $"El email {email} ya se usa en otro cliente");
        }

        var tenant = new Tenant { Id = Guid.NewGuid(), Codigo = codigo!, Nombre = nombre, Estado = EstadosTenant.Activo, CreadoEn = DateTimeOffset.UtcNow };
        try
        {
            await using (var db = bases.Tenant(tenant.Codigo))
            {
                await DbInitializer.SeedAsync(db, datosDePrueba: false);
                db.Empresas.Add(new Empresa { Id = Guid.NewGuid(), Nombre = nombre, Activa = true });
                var admin = new Usuario { Id = Guid.NewGuid(), Email = email, Nombre = dto.AdminNombre?.Trim() is { Length: > 0 } n ? n : "Administrador", Rol = Roles.Admin, Activo = true };
                admin.PasswordHash = hasher.HashPassword(admin, dto.AdminPassword!);
                db.Usuarios.Add(admin);
                var clienteNombre = await db.Configuraciones.FirstAsync(c => c.Clave == ConfigKeys.ClienteNombre && c.EmpresaId == null);
                clienteNombre.Valor = nombre;
                await db.SaveChangesAsync();
            }

            central.Tenants.Add(tenant);
            central.Logins.Add(new LoginTenant { Email = email, TenantId = tenant.Id });
            await central.SaveChangesAsync();
            log.LogInformation("Alta de cliente {Codigo} ({Nombre})", tenant.Codigo, tenant.Nombre);
            return (tenant, null);
        }
        catch (Exception ex)
        {
            // Si no quedó registrado en la central, la base a medio hacer no sirve: se borra.
            log.LogError(ex, "Falló el alta del cliente {Codigo}", tenant.Codigo);
            try { await bases.BorrarBaseAsync(tenant.Codigo); }
            catch (Exception borrar) { log.LogError(borrar, "No se pudo borrar la base a medio crear de {Codigo}", tenant.Codigo); }
            return (null, "No se pudo crear la base del cliente: " + ex.Message);
        }
    }
}
