using Bfs.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bfs.Infrastructure.Data;

public class BfsDbContext : DbContext
{
    public BfsDbContext(DbContextOptions<BfsDbContext> options) : base(options) { }

    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Transporte> Transportes => Set<Transporte>();
    public DbSet<Exportador> Exportadores => Set<Exportador>();
    public DbSet<TaraConocida> Taras => Set<TaraConocida>();
    public DbSet<DatosVehiculo> DatosVehiculos => Set<DatosVehiculo>();
    public DbSet<Balanza> Balanzas => Set<Balanza>();
    public DbSet<ModeloBalanza> ModelosBalanza => Set<ModeloBalanza>();
    public DbSet<ContadorTicket> ContadoresTicket => Set<ContadorTicket>();
    public DbSet<Pesada> Pesadas => Set<Pesada>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurarSync<Empresa>(modelBuilder);
        ConfigurarSync<Usuario>(modelBuilder);
        ConfigurarSync<Configuracion>(modelBuilder);
        ConfigurarSync<Cliente>(modelBuilder);
        ConfigurarSync<Producto>(modelBuilder);
        ConfigurarSync<Transporte>(modelBuilder);
        ConfigurarSync<Pesada>(modelBuilder);
        ConfigurarSync<Exportador>(modelBuilder);
        ConfigurarSync<TaraConocida>(modelBuilder);
        ConfigurarSync<DatosVehiculo>(modelBuilder);
        ConfigurarSync<Balanza>(modelBuilder);
        ConfigurarSync<ModeloBalanza>(modelBuilder);
        ConfigurarSync<ContadorTicket>(modelBuilder);

        modelBuilder.Entity<Empresa>().HasIndex(e => e.Nombre).IsUnique();
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<Usuario>().HasOne(u => u.Cliente).WithMany().HasForeignKey(u => u.ClienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pesada>().HasOne<Cliente>().WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Pesada>().HasIndex(p => new { p.ClienteId, p.EntradaEn });
        modelBuilder.Entity<Configuracion>().HasIndex(c => new { c.Clave, c.EmpresaId }).IsUnique();
        modelBuilder.Entity<Cliente>().HasIndex(c => c.RazonSocial);
        modelBuilder.Entity<Producto>().HasIndex(p => p.Nombre).IsUnique();
        modelBuilder.Entity<Transporte>().HasIndex(t => t.RazonSocial);
        modelBuilder.Entity<Exportador>().HasIndex(e => e.RazonSocial).IsUnique();
        modelBuilder.Entity<TaraConocida>().HasIndex(t => t.Identificador).IsUnique();
        modelBuilder.Entity<DatosVehiculo>().HasIndex(d => d.PatenteChasis).IsUnique();
        modelBuilder.Entity<Balanza>().HasIndex(b => b.Nombre).IsUnique();
        modelBuilder.Entity<ModeloBalanza>().HasIndex(m => m.Nombre).IsUnique();
        modelBuilder.Entity<ContadorTicket>().HasIndex(c => new { c.EmpresaId, c.Puesto }).IsUnique();
        modelBuilder.Entity<Pesada>().HasIndex(p => new { p.Estado, p.EntradaEn });
        modelBuilder.Entity<Pesada>().OwnsOne(p => p.Aduana);
        modelBuilder.Entity<Pesada>().HasIndex(p => new { p.EmpresaId, p.TicketNumero }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.Estado, o.ProximoIntento });
        modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.Destino, o.TicketNumero, o.Evento });

        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<Pesada>().Property(p => p.DatosExtraJson).HasColumnType("jsonb");
        }

        if (Database.IsSqlite())
        {
            // SQLite no ordena ni compara DateTimeOffset; guardado como entero conserva el orden por UTC.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
                }
            }
        }
    }

    private static void ConfigurarSync<T>(ModelBuilder modelBuilder) where T : EntidadSync
    {
        var entity = modelBuilder.Entity<T>();
        entity.Property(e => e.Version).IsConcurrencyToken();
        entity.HasQueryFilter(e => !e.Eliminado);
        entity.HasIndex(e => e.ModificadoEn);
    }
}
