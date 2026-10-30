using Microsoft.Data.Sqlite;

namespace Bfs.Infrastructure.Data;

/// <summary>Copias de la base SQLite del nodo: backup, espejo y restauración.</summary>
public static class ArchivosBase
{
    /// <summary>
    /// Copia consistente aunque el sistema esté pesando (VACUUM INTO, sin cerrar la base).
    /// Se escribe a un temporal y después se reemplaza: si se corta a la mitad, la copia anterior queda sana.
    /// </summary>
    public static void Copiar(string baseOrigen, string destino)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
        var temporal = destino + ".tmp";
        if (File.Exists(temporal)) File.Delete(temporal);

        using (var conn = new SqliteConnection(BfsDatabase.SqliteConnectionString(baseOrigen)))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM INTO $destino";
            cmd.Parameters.AddWithValue("$destino", temporal);
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearPool(new SqliteConnection(BfsDatabase.SqliteConnectionString(temporal)));
        File.Move(temporal, destino, overwrite: true);
    }

    /// <returns>null si es una base de la 4.0; si no, por qué no se puede restaurar.</returns>
    public static string? Validar(string archivo)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('__EFMigrationsHistory', 'Pesadas', 'Usuarios')";
            if (Convert.ToInt64(cmd.ExecuteScalar()) != 3)
            {
                return "El archivo no es un backup del sistema de pesaje 4.0 (para la base de la 3.x usá Importar)";
            }
            cmd.CommandText = "PRAGMA quick_check";
            return cmd.ExecuteScalar() as string == "ok" ? null : "El archivo está dañado";
        }
        catch (SqliteException)
        {
            return "El archivo no es una base de datos válida";
        }
    }

    /// <summary>
    /// Si quedó una restauración pendiente, la base actual se guarda en backups y la subida pasa a ser la base.
    /// Se llama al arrancar, antes de abrir la base.
    /// </summary>
    /// <returns>Dónde quedó la base anterior, o null si no había nada para restaurar.</returns>
    public static string? AplicarRestauracionPendiente(string restaurar, string baseActual, string carpetaBackups)
    {
        if (!File.Exists(restaurar)) return null;
        string? anterior = null;
        if (File.Exists(baseActual))
        {
            anterior = Path.Combine(carpetaBackups, $"antes-de-restaurar-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            Directory.CreateDirectory(carpetaBackups);
            Copiar(baseActual, anterior);
            SqliteConnection.ClearAllPools();
            foreach (var sufijo in new[] { "", "-wal", "-shm" })
            {
                if (File.Exists(baseActual + sufijo)) File.Delete(baseActual + sufijo);
            }
        }
        File.Move(restaurar, baseActual);
        return anterior ?? baseActual;
    }
}
