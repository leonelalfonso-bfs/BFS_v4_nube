using Microsoft.Data.Sqlite;

namespace Bfs.Infrastructure.Data;

/// <summary>
/// Las bases del spike se crearon con EnsureCreated y no tienen historial de migraciones,
/// así que Migrate fallaría sobre ellas. Se apartan como respaldo (nunca se borran) y se arranca una base nueva.
/// </summary>
public static class SpikeDbRespaldo
{
    /// <returns>Ruta del respaldo, o null si no hizo falta.</returns>
    public static string? ApartarSiNoTieneMigraciones(string dbFile)
    {
        if (!File.Exists(dbFile)) return null;

        using (var conn = new SqliteConnection(BfsDatabase.SqliteConnectionString(dbFile)))
        {
            conn.Open();
            if (TablaExiste(conn, "__EFMigrationsHistory") || !HayTablas(conn))
            {
                return null;
            }

            using var checkpoint = conn.CreateCommand();
            checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpoint.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        var respaldo = Path.Combine(
            Path.GetDirectoryName(dbFile)!,
            $"{Path.GetFileNameWithoutExtension(dbFile)}.spike-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        File.Move(dbFile, respaldo);
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(dbFile + suffix)) File.Move(dbFile + suffix, respaldo + suffix);
        }
        return respaldo;
    }

    private static bool TablaExiste(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static bool HayTablas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}
