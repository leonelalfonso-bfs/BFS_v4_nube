using Bfs.Core.Entities;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<Pesada> Pesadas => Set<Pesada>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>().HasIndex(e => e.Nombre).IsUnique();
        modelBuilder.Entity<Usuario>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Configuracion>().HasIndex(c => new { c.Clave, c.EmpresaId }).IsUnique();
        modelBuilder.Entity<Cliente>().HasIndex(c => c.RazonSocial);
        modelBuilder.Entity<Producto>().HasIndex(p => p.Nombre).IsUnique();
        modelBuilder.Entity<Pesada>().HasIndex(p => new { p.EmpresaId, p.TicketNumero }).IsUnique();
        modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.Estado, o.ProximoIntento });
        modelBuilder.Entity<OutboxMessage>().HasIndex(o => new { o.Destino, o.TicketNumero, o.Evento });
    }
}
