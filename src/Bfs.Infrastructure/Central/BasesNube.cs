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
    public const string PrefijoPorDefecto = "bfs_";

    private readonly ProveedorBase _proveedor;
    private readonly string _prefijo;
    private readonly string? _servidor;
    private readonly string _carpeta;
    private readonly ConcurrentDictionary<string, DbContextOptions<BfsDbContext>> _opciones = new();
    private readonly SyncStampInterceptor _interceptor = new(NodoInfo.Ninguno, marcarSecuencia: true);

    /// <param name="servidor">Cadena de PostgreSQL (la base que diga se reemplaza por la central o la del cliente).</param>
    /// <param name="carpeta">Para SQLite: dónde van central.db y tenants/*.db.</param>
    /// <param name="prefijo">
    /// Las bases se llaman <c>{prefijo}central</c> y <c>{prefijo}t_{código}</c>. Por defecto "bfs_";
    /// otro prefijo permite tener una nube de prueba en el mismo servidor (y a los tests correr en paralelo).
    /// </param>
    public BasesNube(ProveedorBase proveedor, string? servidor, string carpeta, string prefijo = PrefijoPorDefecto)
    {
        if (proveedor == ProveedorBase.Postgres && string.IsNullOrWhiteSpace(servidor))
        {
            throw new InvalidOperationException("Modo Nube con PostgreSQL requiere ConnectionStrings:Bfs.");
        }
        if (!Prefijo().IsMatch(prefijo)) throw new ArgumentException($"Prefijo de bases inválido: {prefijo}");
        _proveedor = proveedor;
        _prefijo = prefijo;
        _servidor = servidor;
        _carpeta = carpeta;
    }

    public ProveedorBase Proveedor => _proveedor;
    public string BaseCentral => _prefijo + "central";
    public string BaseTenant(string codigo) => _prefijo + "t_" + codigo;

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
        if (_proveedor == ProveedorBase.Postgres) return ConBase(BaseTenant(codigo));
        var carpeta = Path.Combine(_carpeta, "tenants");
        Directory.CreateDirectory(carpeta);
        return BfsDatabase.SqliteConnectionString(Path.Combine(carpeta, codigo + ".db"));
    }

    private string ConBase(string nombre) =>
        new NpgsqlConnectionStringBuilder(_servidor) { Database = nombre }.ConnectionString;

    /// <summary>Borra la central y todas las bases de clientes de este prefijo. Solo para limpiar después de los tests.</summary>
    public async Task BorrarTodoAsync()
    {
        if (_proveedor == ProveedorBase.Sqlite)
        {
            SqliteConnection.ClearAllPools();
            return;
        }
        await using var conn = new NpgsqlConnection(ConBase("postgres"));
        await conn.OpenAsync();
        var bases = new List<string>();
        await using (var cmd = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE @p", conn))
        {
            // En LIKE el "_" es comodín: se escapa para que "bfs_" no traiga también "bfsx...".
            cmd.Parameters.AddWithValue("p", _prefijo.Replace("_", @"\_") + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) bases.Add(r.GetString(0));
        }
        NpgsqlConnection.ClearAllPools();
        foreach (var b in bases)
        {
            // El nombre sale de pg_database y respeta el prefijo validado: no hay entrada del usuario acá.
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{b}\" WITH (FORCE)", conn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_]{1,39}$")]
    private static partial Regex Codigo();

    [GeneratedRegex("^[a-z][a-z0-9_]{0,19}$")]
    private static partial Regex Prefijo();
}
