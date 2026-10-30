using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bfs.Infrastructure.Postgres;

/// <summary>Solo para `dotnet ef migrations add ... --context BfsCentralContext`; no abre la base.</summary>
public class DesignTimeFactoryCentral : IDesignTimeDbContextFactory<BfsCentralContext>
{
    public BfsCentralContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BfsCentralContext>();
        BfsDatabase.Configurar(options, ProveedorBase.Postgres, "Host=localhost;Database=bfs_central");
        return new BfsCentralContext(options.Options);
    }
}
