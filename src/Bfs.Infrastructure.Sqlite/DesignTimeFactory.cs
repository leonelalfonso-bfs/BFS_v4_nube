using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bfs.Infrastructure.Sqlite;

/// <summary>Solo para `dotnet ef migrations add`; no abre la base.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<BfsDbContext>
{
    public BfsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(options, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString("design.db"));
        return new BfsDbContext(options.Options);
    }
}
