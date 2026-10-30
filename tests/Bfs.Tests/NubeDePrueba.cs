using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Data;

namespace Bfs.Tests;

/// <summary>
/// Motor de los tests de la nube. Con la variable <c>BFS_TEST_POSTGRES</c> (cadena de conexión) corren contra ese
/// PostgreSQL, cada clase con su propio prefijo de bases; sin ella, con un archivo SQLite por cliente.
/// En el CI corre de las dos formas.
/// </summary>
public sealed class NubeDePrueba
{
    public static readonly string? Postgres = Environment.GetEnvironmentVariable("BFS_TEST_POSTGRES");

    public string Prefijo { get; } = $"t{Guid.NewGuid():N}"[..9] + "_";

    public bool EsPostgres => !string.IsNullOrWhiteSpace(Postgres);

    public IEnumerable<string> Args() => EsPostgres
        ? ["--Bfs:Nube:Proveedor=Postgres", $"--ConnectionStrings:Bfs={Postgres}", $"--Bfs:Nube:PrefijoBases={Prefijo}"]
        : ["--Bfs:Nube:Proveedor=Sqlite"];

    public Task LimpiarAsync(string carpeta) =>
        new BasesNube(EsPostgres ? ProveedorBase.Postgres : ProveedorBase.Sqlite, Postgres, carpeta, Prefijo).BorrarTodoAsync();
}
