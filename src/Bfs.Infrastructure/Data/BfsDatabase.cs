using Bfs.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bfs.Infrastructure.Data;

/// <summary>Motor de base. El nodo usa SQLite; la nube, PostgreSQL (o SQLite para desarrollar y testear sin servidor).</summary>
public enum ProveedorBase
{
    Sqlite,
    Postgres
}

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
        Configurar(o, modo == ModoDespliegue.Nodo ? ProveedorBase.Sqlite : ProveedorBase.Postgres, connectionString);

    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder o, ProveedorBase proveedor, string connectionString) =>
        proveedor switch
        {
            ProveedorBase.Sqlite => o.UseSqlite(connectionString, x => x.MigrationsAssembly(MigracionesSqlite)),
            ProveedorBase.Postgres => o.UseNpgsql(connectionString, x => x.MigrationsAssembly(MigracionesPostgres)),
            _ => throw new ArgumentOutOfRangeException(nameof(proveedor), proveedor, null)
        };

    public static string SqliteConnectionString(string file) => $"Data Source={file}";
}
