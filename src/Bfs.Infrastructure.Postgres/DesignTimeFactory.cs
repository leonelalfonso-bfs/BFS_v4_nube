using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bfs.Infrastructure.Postgres;

/// <summary>Solo para `dotnet ef migrations add`; no abre la base.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<BfsDbContext>
{
    public BfsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(options, ModoDespliegue.Nube, "Host=localhost;Database=bfs_design");
        return new BfsDbContext(options.Options);
    }
}
