using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bfs.Infrastructure.Central;

/// <summary>
/// Dónde está la base central y la de cada cliente.
/// PostgreSQL: el mismo servidor, una base por cliente (<c>bfs_t_{codigo}</c>), con backups y restauración por separado.
/// SQLite: un archivo por cliente; solo para desarrollar y testear sin servidor.
/// </summary>
public sealed partial class BasesNube
{
    public const string BaseCentral = "bfs_central";
    public const string PrefijoTenant = "bfs_t_";

    private readonly ProveedorBase _proveedor;
    private readonly string? _servidor;
    private readonly string _carpeta;
    private readonly ConcurrentDictionary<string, DbContextOptions<BfsDbContext>> _opciones = new();
    private readonly SyncStampInterceptor _interceptor = new(NodoInfo.Ninguno);

    /// <param name="servidor">Cadena de PostgreSQL (la base que diga se reemplaza por la central o la del cliente).</param>
    /// <param name="carpeta">Para SQLite: dónde van central.db y tenants/*.db.</param>
    public BasesNube(ProveedorBase proveedor, string? servidor, string carpeta)
    {
        if (proveedor == ProveedorBase.Postgres && string.IsNullOrWhiteSpace(servidor))
        {
            throw new InvalidOperationException("Modo Nube con PostgreSQL requiere ConnectionStrings:Bfs.");
        }
        _proveedor = proveedor;
        _servidor = servidor;
        _carpeta = carpeta;
    }

    public ProveedorBase Proveedor => _proveedor;

    public static bool CodigoValido(string? codigo) => codigo != null && Codigo().IsMatch(codigo);

    public DbContextOptions<BfsCentralContext> OpcionesCentral()
    {
        var o = new DbContextOptionsBuilder<BfsCentralContext>();
        var cs = _proveedor == ProveedorBase.Postgres
            ? ConBase(BaseCentral)
            : BfsDatabase.SqliteConnectionString(Path.Combine(_carpeta, "central.db"));
        BfsDatabase.Configurar(o, _proveedor, cs);
        return o.Options;
    }

    public BfsCentralContext Central() => new(OpcionesCentral());

    public BfsDbContext Tenant(string codigo)
    {
        if (!CodigoValido(codigo)) throw new ArgumentException($"Código de cliente inválido: {codigo}");
        var opciones = _opciones.GetOrAdd(codigo, c =>
        {
            var o = new DbContextOptionsBuilder<BfsDbContext>();
            BfsDatabase.Configurar(o, _proveedor, CadenaTenant(c));
            o.AddInterceptors(_interceptor);
            return o.Options;
        });
        return new BfsDbContext(opciones);
    }

    /// <summary>Borra la base de un cliente cuyo alta falló a mitad de camino (no se usa para bajas: esas conservan los datos).</summary>
    public async Task BorrarBaseAsync(string codigo)
    {
        await using var db = Tenant(codigo);
        await db.Database.EnsureDeletedAsync();
        _opciones.TryRemove(codigo, out _);
        if (_proveedor == ProveedorBase.Sqlite) SqliteConnection.ClearAllPools();
    }

    private string CadenaTenant(string codigo)
    {
        if (_proveedor == ProveedorBase.Postgres) return ConBase(PrefijoTenant + codigo);
        var carpeta = Path.Combine(_carpeta, "tenants");
        Directory.CreateDirectory(carpeta);
        return BfsDatabase.SqliteConnectionString(Path.Combine(carpeta, codigo + ".db"));
    }

    private string ConBase(string nombre) =>
        new NpgsqlConnectionStringBuilder(_servidor) { Database = nombre }.ConnectionString;

    [GeneratedRegex("^[a-z0-9][a-z0-9_]{1,39}$")]
    private static partial Regex Codigo();
}
