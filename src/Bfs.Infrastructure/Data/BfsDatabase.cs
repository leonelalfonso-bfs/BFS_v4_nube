using Bfs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bfs.Infrastructure.Data;

/// <summary>
/// Elige el proveedor según el modo. Cada proveedor tiene sus propias migraciones en un ensamblado aparte.
/// </summary>
public static class BfsDatabase
{
    public const string MigracionesSqlite = "Bfs.Infrastructure.Sqlite";
    public const string MigracionesPostgres = "Bfs.Infrastructure.Postgres";

    public static IServiceCollection AddBfsDatabase(this IServiceCollection services, ModoDespliegue modo, string connectionString)
    {
        services.AddSingleton<SyncStampInterceptor>();
        services.AddDbContext<BfsDbContext>((sp, o) =>
        {
            Configurar(o, modo, connectionString);
            o.AddInterceptors(sp.GetRequiredService<SyncStampInterceptor>());
        });
        return services;
    }

    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder o, ModoDespliegue modo, string connectionString) =>
        modo switch
        {
            ModoDespliegue.Nodo => o.UseSqlite(connectionString, x => x.MigrationsAssembly(MigracionesSqlite)),
            ModoDespliegue.Nube => o.UseNpgsql(connectionString, x => x.MigrationsAssembly(MigracionesPostgres)),
            _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, null)
        };

    public static string SqliteConnectionString(string file) => $"Data Source={file}";
}
