using Microsoft.Data.Sqlite;

namespace Bfs.Tests;

public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bfs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { /* el log de Serilog puede seguir abierto */ }
    }
}
