using Bfs.Core;
using Bfs.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Infrastructure.Data;

public static class DbInitializer
{
    public const string SeedVendorEmail = "bfs@bfs.local";
    public const string SeedAdminEmail = "admin@bfs.local";

    public static async Task SeedAsync(BfsDbContext db)
    {
        await db.Database.MigrateAsync();

        if (!await db.Empresas.AnyAsync())
        {
            db.Empresas.AddRange(
                new Empresa { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Nombre = "Empresa 1", Activa = true, ProximoTicket = 1 },
                new Empresa { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Nombre = "Empresa 2", Activa = true, ProximoTicket = 1 }
            );
        }

        if (!await db.Usuarios.AnyAsync())
        {
            var hasher = new PasswordHasher<Usuario>();
            var vendor = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = SeedVendorEmail,
                Rol = Roles.Vendor,
                Nombre = "BFS",
                Activo = true
            };
            vendor.PasswordHash = hasher.HashPassword(vendor, "bfs-setup");

            var admin = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = SeedAdminEmail,
                Rol = Roles.Admin,
                Nombre = "Administrador",
                Activo = true
            };
            admin.PasswordHash = hasher.HashPassword(admin, "admin");

            db.Usuarios.AddRange(vendor, admin);
        }

        await EnsureConfig(db, ConfigKeys.ClienteNombre, "Cliente de prueba");
        await EnsureConfig(db, ConfigKeys.Aduana, "false");
        await EnsureConfig(db, ConfigKeys.Contenedores, "false");
        await EnsureConfig(db, ConfigKeys.TaraConocida, "false");
        await EnsureConfig(db, ConfigKeys.TaraPallets, "true");
        await EnsureConfig(db, ConfigKeys.Termica, "false");
        await EnsureConfig(db, ConfigKeys.TermicaImpresora, "");
        await EnsureConfig(db, ConfigKeys.TermicaAncho, "80");
        await EnsureConfig(db, ConfigKeys.IngresoManual, "true");
        await EnsureConfig(db, ConfigKeys.WormsEnabled, "false");
        await EnsureConfig(db, ConfigKeys.WormsEndpoint, "");
        await EnsureConfig(db, ConfigKeys.WormsApiKey, "");
        await EnsureConfig(db, ConfigKeys.WormsBalanza, "1");
        await EnsureConfig(db, ConfigKeys.WormsTipoDefault, "INGRESO");
        await EnsureConfig(db, ConfigKeys.ScaleMode, "sim");
        await EnsureConfig(db, ConfigKeys.ScalePort, "");
        await EnsureConfig(db, ConfigKeys.ScaleBaud, "9600");
        await EnsureConfig(db, ConfigKeys.ScaleDataBits, "8");
        await EnsureConfig(db, ConfigKeys.ScaleStopBits, "1");
        await EnsureConfig(db, ConfigKeys.ScaleParity, "None");
        await EnsureConfig(db, ConfigKeys.ScaleProtocol, "Line");
        await EnsureConfig(db, ConfigKeys.ScaleExtract, "");
        await EnsureConfig(db, ConfigKeys.FieldsJson, FieldCatalog.DefaultJson());
        await EnsureConfig(db, ConfigKeys.TicketTitulo, "Comprobante de pesaje");
        await EnsureConfig(db, ConfigKeys.TicketPie, "Balanzas Full Service");
        await EnsureConfig(db, ConfigKeys.LogoFile, "");

        await db.SaveChangesAsync();
    }

    private static async Task EnsureConfig(BfsDbContext db, string clave, string valor)
    {
        var exists = await db.Configuraciones.AnyAsync(c => c.Clave == clave && c.EmpresaId == null);
        if (!exists)
        {
            db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = clave, Valor = valor, EmpresaId = null });
        }
    }
}
