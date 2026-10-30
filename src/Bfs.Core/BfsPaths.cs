namespace Bfs.Core;

public sealed class BfsPaths
{
    public BfsPaths(string? root)
    {
        Root = string.IsNullOrWhiteSpace(root) ? DefaultRoot() : root;
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string Logs => Sub("logs");
    public string Branding => Sub("branding");
    public string Backups => Sub("backups");

    /// <summary>Base subida para restaurar: se aplica al próximo arranque, antes de abrir la base.</summary>
    public string Restaurar => Path.Combine(Root, "restaurar.db");
    public string BaseSqlite => Path.Combine(Root, "bfs.db");

    /// <summary>Id del puesto, creado la primera vez que arranca el nodo.</summary>
    public Guid NodoId()
    {
        var file = Path.Combine(Root, "nodo.id");
        if (File.Exists(file) && Guid.TryParse(File.ReadAllText(file).Trim(), out var id))
        {
            return id;
        }

        id = Guid.NewGuid();
        File.WriteAllText(file, id.ToString());
        return id;
    }

    private string Sub(string name)
    {
        var dir = Path.Combine(Root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string DefaultRoot()
    {
        var programData = Environment.GetEnvironmentVariable("ProgramData") ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "bfs-4");
    }
}
