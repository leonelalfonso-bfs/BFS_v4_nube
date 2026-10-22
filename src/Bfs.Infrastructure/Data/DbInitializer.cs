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
        await EnsureConfig(db, ConfigKeys.Puesto, "1");
        await EnsureConfig(db, ConfigKeys.ExigirEstable, "true");
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

        await SembrarModelos(db);
        await PasarBalanzaDelSpike(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Los modelos de <c>default_models.sql</c> de la 3.x.</summary>
    public static IReadOnlyList<ModeloBalanza> ModelosDe3x { get; } =
    [
        Modelo("Moretti W-180", 9600, "5:10", 7, "Even", ProtocolosBalanza.Stx),
        Modelo("Sipel Aries", 9600, "14:25", 8, "None", ProtocolosBalanza.Stx),
        Modelo("Gama A12", 9600, "1:7", 8, "None", ProtocolosBalanza.Line),
        Modelo("IND-100", 9600, "1:6", 8, "None", ProtocolosBalanza.Stx),
        Modelo("CONDEC", 9600, "1:9", 8, "None", ProtocolosBalanza.Stx),
        Modelo("EL05", 9600, "2:7", 8, "None", ProtocolosBalanza.Line),
        Modelo("GaMa 2002e", 9600, "8:14", 8, "None", ProtocolosBalanza.Line),
        Modelo("INGAPSA 768", 1200, "2:7", 8, "None", ProtocolosBalanza.Line)
    ];

    private static ModeloBalanza Modelo(string nombre, int baudios, string extraccion, int dataBits, string paridad, string protocolo) =>
        new() { Nombre = nombre, Baudios = baudios, Extraccion = extraccion, DataBits = dataBits, Paridad = paridad, StopBits = 1, Protocolo = protocolo };

    private static async Task SembrarModelos(BfsDbContext db)
    {
        if (await db.ModelosBalanza.IgnoreQueryFilters().AnyAsync()) return;
        foreach (var m in ModelosDe3x)
        {
            db.ModelosBalanza.Add(new ModeloBalanza
            {
                Id = Guid.NewGuid(), Nombre = m.Nombre, Baudios = m.Baudios, DataBits = m.DataBits, Paridad = m.Paridad,
                StopBits = m.StopBits, Protocolo = m.Protocolo, Extraccion = m.Extraccion
            });
        }
    }

    /// <summary>
    /// El spike guardaba una sola balanza en claves sueltas (scale_*). La primera vez se pasa a la tabla de balanzas;
    /// en una base nueva queda una balanza simulada para poder probar.
    /// </summary>
    private static async Task PasarBalanzaDelSpike(BfsDbContext db)
    {
        if (await db.Balanzas.IgnoreQueryFilters().AnyAsync()) return;
        var c = await db.Configuraciones.Where(x => x.EmpresaId == null).ToDictionaryAsync(x => x.Clave, x => x.Valor);
        int Num(string k, int def) => int.TryParse(c.GetValueOrDefault(k), out var v) ? v : def;
        var serie = c.GetValueOrDefault(ConfigKeys.ScaleMode) == ModosBalanza.Serie && !string.IsNullOrWhiteSpace(c.GetValueOrDefault(ConfigKeys.ScalePort));

        db.Balanzas.Add(new Balanza
        {
            Id = Guid.NewGuid(),
            Nombre = "Balanza 1",
            Modo = serie ? ModosBalanza.Serie : ModosBalanza.Simulada,
            Puerto = c.GetValueOrDefault(ConfigKeys.ScalePort, ""),
            Baudios = Num(ConfigKeys.ScaleBaud, 9600),
            DataBits = Num(ConfigKeys.ScaleDataBits, 8),
            StopBits = Num(ConfigKeys.ScaleStopBits, 1),
            Paridad = c.GetValueOrDefault(ConfigKeys.ScaleParity, "None"),
            Protocolo = c.GetValueOrDefault(ConfigKeys.ScaleProtocol, ProtocolosBalanza.Line),
            Extraccion = c.GetValueOrDefault(ConfigKeys.ScaleExtract),
            Orden = 1
        });
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
