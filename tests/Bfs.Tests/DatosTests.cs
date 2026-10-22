using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

public class DatosTests
{
    private static readonly Guid Nodo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private static BfsDbContext Sqlite(string file, TimeProvider? clock = null)
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(file));
        o.AddInterceptors(new SyncStampInterceptor(new NodoInfo(Nodo), clock));
        return new BfsDbContext(o.Options);
    }

    private static BfsDbContext Postgres()
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nube, "Host=localhost;Database=bfs_sin_conexion");
        return new BfsDbContext(o.Options);
    }

    [Fact]
    public void Migraciones_sqlite_al_dia_con_el_modelo()
    {
        using var tmp = new TempDir();
        using var db = Sqlite(tmp.File("x.db"));
        Assert.False(db.Database.HasPendingModelChanges(), "Falta generar una migración SQLite");
    }

    [Fact]
    public void Migraciones_postgres_al_dia_con_el_modelo()
    {
        using var db = Postgres();
        Assert.False(db.Database.HasPendingModelChanges(), "Falta generar una migración PostgreSQL");
        Assert.NotEmpty(db.Database.GetMigrations());
    }

    [Fact]
    public async Task Migrar_y_sembrar_una_base_nueva()
    {
        using var tmp = new TempDir();
        await using var db = Sqlite(tmp.File("bfs.db"));
        await DbInitializer.SeedAsync(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(2, await db.Empresas.CountAsync());
        Assert.True(await db.Usuarios.AnyAsync(u => u.Username == "admin"));
        Assert.All(await db.Empresas.ToListAsync(), e =>
        {
            Assert.Equal(1, e.Version);
            Assert.Equal(Nodo, e.OrigenNodoId);
        });

        // Sembrar dos veces no duplica nada.
        await DbInitializer.SeedAsync(db);
        Assert.Equal(2, await db.Empresas.CountAsync());
    }

    [Fact]
    public async Task Guardar_completa_columnas_de_sync_y_borrar_es_logico()
    {
        using var tmp = new TempDir();
        var clock = new RelojFijo(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));
        var file = tmp.File("bfs.db");
        Guid id;

        await using (var db = Sqlite(file, clock))
        {
            await db.Database.MigrateAsync();
            var cliente = new Cliente { RazonSocial = "Agro SA" };
            db.Clientes.Add(cliente);
            await db.SaveChangesAsync();
            id = cliente.Id;

            Assert.NotEqual(Guid.Empty, id);
            Assert.Equal(1, cliente.Version);
            Assert.Equal(clock.Ahora, cliente.CreadoEn);
            Assert.Equal(Nodo, cliente.OrigenNodoId);

            clock.Ahora = clock.Ahora.AddMinutes(5);
            cliente.Cuit = "30-00000000-0";
            await db.SaveChangesAsync();
            Assert.Equal(2, cliente.Version);
            Assert.Equal(clock.Ahora, cliente.ModificadoEn);

            db.Clientes.Remove(cliente);
            await db.SaveChangesAsync();
        }

        await using (var db = Sqlite(file, clock))
        {
            Assert.False(await db.Clientes.AnyAsync(c => c.Id == id));
            var borrado = await db.Clientes.IgnoreQueryFilters().SingleAsync(c => c.Id == id);
            Assert.True(borrado.Eliminado);
            Assert.Equal(3, borrado.Version);
        }
    }

    [Fact]
    public async Task Version_detecta_ediciones_concurrentes()
    {
        using var tmp = new TempDir();
        var file = tmp.File("bfs.db");
        await using (var db = Sqlite(file))
        {
            await DbInitializer.SeedAsync(db);
        }

        await using var a = Sqlite(file);
        await using var b = Sqlite(file);
        var ea = await a.Empresas.OrderBy(e => e.Nombre).FirstAsync();
        var eb = await b.Empresas.SingleAsync(e => e.Id == ea.Id);

        ea.ProximoTicket++;
        await a.SaveChangesAsync();

        eb.ProximoTicket++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task Ordenar_por_fecha_funciona_en_sqlite()
    {
        using var tmp = new TempDir();
        await using var db = Sqlite(tmp.File("bfs.db"));
        await DbInitializer.SeedAsync(db);
        var empresa = await db.Empresas.FirstAsync();
        var t0 = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        db.Pesadas.AddRange(
            new Pesada { EmpresaId = empresa.Id, TicketNumero = "1", EntradaEn = t0 },
            new Pesada { EmpresaId = empresa.Id, TicketNumero = "2", EntradaEn = t0.AddHours(2) },
            new Pesada { EmpresaId = empresa.Id, TicketNumero = "3", EntradaEn = t0.AddHours(1) });
        await db.SaveChangesAsync();

        var tickets = await db.Pesadas
            .Where(p => p.EntradaEn > t0)
            .OrderByDescending(p => p.EntradaEn)
            .Select(p => p.TicketNumero)
            .ToListAsync();
        Assert.Equal(["2", "3"], tickets);
    }

    [Fact]
    public void Base_del_spike_sin_migraciones_se_aparta_sin_borrarla()
    {
        using var tmp = new TempDir();
        var file = tmp.File("bfs.db");
        using (var conn = new SqliteConnection(BfsDatabase.SqliteConnectionString(file)))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE Empresas (Id TEXT); INSERT INTO Empresas VALUES ('x');";
            cmd.ExecuteNonQuery();
        }

        var respaldo = SpikeDbRespaldo.ApartarSiNoTieneMigraciones(file);

        Assert.NotNull(respaldo);
        Assert.False(File.Exists(file));
        Assert.True(File.Exists(respaldo));
    }

    [Fact]
    public async Task Base_con_migraciones_no_se_toca()
    {
        using var tmp = new TempDir();
        var file = tmp.File("bfs.db");
        await using (var db = Sqlite(file))
        {
            await db.Database.MigrateAsync();
        }

        Assert.Null(SpikeDbRespaldo.ApartarSiNoTieneMigraciones(file));
        Assert.True(File.Exists(file));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
