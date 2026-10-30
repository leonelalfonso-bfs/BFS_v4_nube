using Microsoft.EntityFrameworkCore;

namespace Bfs.Infrastructure.Central;

public static class EstadosTenant
{
    public const string Activo = "Activo";
    public const string Suspendido = "Suspendido";
    public const string Baja = "Baja";

    public static IReadOnlyList<string> Todos { get; } = [Activo, Suspendido, Baja];
}

/// <summary>Un cliente de BFS en la nube. Sus datos viven en su propia base (<c>bfs_t_{Codigo}</c>).</summary>
public class Tenant
{
    public Guid Id { get; set; }

    /// <summary>Minúsculas, números y guiones bajos; arma el nombre de la base. No cambia nunca.</summary>
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Estado { get; set; } = EstadosTenant.Activo;
    public string? MotivoEstado { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? EstadoDesde { get; set; }
    public string? Notas { get; set; }
}

/// <summary>Email → tenant. Solo eso: la clave y los datos del usuario están en la base del cliente.</summary>
public class LoginTenant
{
    public string Email { get; set; } = "";
    public Guid TenantId { get; set; }
}

/// <summary>Usuario de BFS que administra los clientes. Vive solo en la base central.</summary>
public class Superadmin
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool Activo { get; set; } = true;
}

/// <summary>PC de portería de un cliente que sincroniza con la nube (Fase 7b).</summary>
public class Nodo
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Nombre { get; set; } = "";
    public string? CodigoActivacionHash { get; set; }
    public DateTimeOffset? CodigoVence { get; set; }
    public string? TokenHash { get; set; }
    public string? Maquina { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ActivadoEn { get; set; }
    public DateTimeOffset? UltimaSincronizacion { get; set; }
}

public class BfsCentralContext : DbContext
{
    public BfsCentralContext(DbContextOptions<BfsCentralContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<LoginTenant> Logins => Set<LoginTenant>();
    public DbSet<Superadmin> Superadmins => Set<Superadmin>();
    public DbSet<Nodo> Nodos => Set<Nodo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Codigo).IsUnique();
        modelBuilder.Entity<LoginTenant>().HasKey(l => l.Email);
        modelBuilder.Entity<LoginTenant>().HasIndex(l => l.TenantId);
        modelBuilder.Entity<LoginTenant>().HasOne<Tenant>().WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Superadmin>().HasIndex(s => s.Email).IsUnique();
        modelBuilder.Entity<Nodo>().HasIndex(n => n.TenantId);
        modelBuilder.Entity<Nodo>().HasOne<Tenant>().WithMany().HasForeignKey(n => n.TenantId).OnDelete(DeleteBehavior.Cascade);

        if (Database.IsSqlite())
        {
            foreach (var p in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                p.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
