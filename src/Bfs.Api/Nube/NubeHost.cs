using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Documents;
using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;
using Bfs.Scale;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Nube;

/// <summary>Opciones de la sección Bfs:Nube.</summary>
public class OpcionesNube
{
    public ProveedorBase Proveedor { get; set; } = ProveedorBase.Postgres;

    /// <summary>Primer superadmin: se crea al arrancar si todavía no hay ninguno (después se puede sacar de la configuración).</summary>
    public string? SuperadminEmail { get; set; }
    public string? SuperadminPassword { get; set; }

    /// <summary>Clave privada (PEM o ruta al archivo) con la que se firma la licencia que se renueva a cada portería.</summary>
    public string? ClaveLicencias { get; set; }

    /// <summary>Cuánto puede estar una portería sin sincronizar (o con el cliente suspendido) antes de dejar de pesar.</summary>
    public int DiasLicencia { get; set; } = 15;
}

public static class NubeHost
{
    public static void AddNube(this WebApplicationBuilder builder, BfsPaths paths)
    {
        var opciones = builder.Configuration.GetSection("Bfs:Nube").Get<OpcionesNube>() ?? new OpcionesNube();
        var bases = new BasesNube(opciones.Proveedor, builder.Configuration.GetConnectionString("Bfs"), paths.Root);

        builder.Services.AddSingleton(opciones);
        builder.Services.AddSingleton(bases);
        builder.Services.AddSingleton(NodoInfo.Ninguno);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<AltaTenants>();
        builder.Services.AddScoped<IIndiceLogins, IndiceLoginsNube>();

        // Cada pedido usa la base de su cliente, según el token.
        builder.Services.AddScoped(sp => sp.GetRequiredService<TenantActual>().Codigo is { } codigo
            ? bases.Tenant(codigo)
            : throw new SinTenantException());

        builder.Services.AddSingleton<IBalanzas, SinBalanzas>();
        builder.Services.AddSingleton<IImpresoraTermica, SinImpresora>();
    }

    /// <summary>
    /// Al arrancar: la base central al día, el primer superadmin y las migraciones de cada cliente
    /// (al actualizar el servidor, todas las bases quedan en la versión nueva).
    /// </summary>
    public static async Task InicializarAsync(IServiceProvider services, ILogger log)
    {
        var bases = services.GetRequiredService<BasesNube>();
        var opciones = services.GetRequiredService<OpcionesNube>();
        await using var central = bases.Central();
        await central.Database.MigrateAsync();

        if (!await central.Superadmins.AnyAsync())
        {
            var email = Usuario.NormalizarEmail(opciones.SuperadminEmail);
            if (email.Length > 0 && (opciones.SuperadminPassword ?? "").Length >= AltaTenants.LargoMinimoClave)
            {
                var hasher = services.GetRequiredService<PasswordHasher<Usuario>>();
                central.Superadmins.Add(new Superadmin { Id = Guid.NewGuid(), Email = email, PasswordHash = hasher.HashPassword(new Usuario(), opciones.SuperadminPassword!) });
                await central.SaveChangesAsync();
                log.LogInformation("Se creó el superadmin {Email}", email);
            }
            else
            {
                log.LogWarning("No hay superadmin: configurá Bfs:Nube:SuperadminEmail y Bfs:Nube:SuperadminPassword (8+ caracteres)");
            }
        }

        foreach (var t in await central.Tenants.AsNoTracking().Where(t => t.Estado != EstadosTenant.Baja).ToListAsync())
        {
            try
            {
                await using var db = bases.Tenant(t.Codigo);
                await DbInitializer.SeedAsync(db, datosDePrueba: false);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "No se pudo actualizar la base del cliente {Codigo}", t.Codigo);
            }
        }
    }
}
