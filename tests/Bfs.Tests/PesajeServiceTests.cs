using Bfs.Api.Config;
using Bfs.Api.Licencias;
using Bfs.Api.Pesadas;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;

namespace Bfs.Tests;

/// <summary>Reglas que dependen de lo que marca la balanza: acá la balanza la maneja el test.</summary>
public class PesajeServiceTests : IAsyncLifetime
{
    private readonly TempDir _tmp = new();
    private readonly BalanzaFija _balanza = new();
    private BfsDbContext _db = null!;
    private PesajeService _pesaje = null!;
    private static readonly Operador Op = new("op@planta.com", PuedePesoManual: false);

    public async Task InitializeAsync()
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(_tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(NodoInfo.Ninguno));
        _db = new BfsDbContext(o.Options);
        await DbInitializer.SeedAsync(_db);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Bfs:Licencia:Requerida"] = "false" }).Build();
        var licencia = new LicenciaService(new BfsOptions(), new BfsPaths(_tmp.Path), config, new HostingEnvironment { EnvironmentName = "Development" });
        _pesaje = new PesajeService(_db, _balanza, new ConfigService(_db), licencia, new PasswordHasher<Usuario>());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _tmp.Dispose();
    }

    private static EntradaRequest Entrada() =>
        new(TestApi.Empresa1, "AB123CD", null, null, null, null, null, null, null, null, null, null, null, null, 0);

    [Fact]
    public async Task Con_peso_estable_toma_el_de_la_balanza()
    {
        _balanza.Marcar(31000, estable: true);
        var r = await _pesaje.RegistrarEntradaAsync(Entrada(), Op);
        Assert.Null(r.Error);
        Assert.Equal(31000, r.Pesada!.PesoEntrada);
        Assert.Equal("Fija", r.Pesada.Balanza);
    }

    [Fact]
    public async Task Peso_inestable_no_se_captura()
    {
        _balanza.Marcar(31000, estable: false);
        var r = await _pesaje.RegistrarEntradaAsync(Entrada(), Op);
        Assert.Equal("El peso todavía no está estable", r.Error);
        Assert.Empty(await _db.Pesadas.ToListAsync());
    }

    [Fact]
    public async Task Si_bfs_no_exige_estable_lo_toma_igual()
    {
        _db.Configuraciones.Single(c => c.Clave == ConfigKeys.ExigirEstable).Valor = "false";
        await _db.SaveChangesAsync();
        _balanza.Marcar(31000, estable: false);
        Assert.Null((await _pesaje.RegistrarEntradaAsync(Entrada(), Op)).Error);
    }

    [Theory]
    [InlineData("error", 0, "no está leyendo")]
    [InlineData("sin señal", 31000, "no está leyendo")]
    [InlineData("abierta", 0, "marca 0 kg")]
    [InlineData("abierta", -20, "marca -20 kg")]
    public async Task No_se_captura_de_una_balanza_que_no_esta_bien(string estado, int kg, string error)
    {
        _balanza.Marcar(kg, estable: true, estado);
        var r = await _pesaje.RegistrarEntradaAsync(Entrada(), Op);
        Assert.Contains(error, r.Error);
    }

    [Fact]
    public async Task La_salida_tambien_exige_estable()
    {
        _balanza.Marcar(31000, estable: true);
        var id = (await _pesaje.RegistrarEntradaAsync(Entrada(), Op)).Pesada!.Id;

        _balanza.Marcar(12000, estable: false);
        Assert.Equal("El peso todavía no está estable", (await _pesaje.RegistrarSalidaAsync(id, new SalidaRequest(null, null), Op)).Error);

        _balanza.Marcar(12000, estable: true);
        var salida = await _pesaje.RegistrarSalidaAsync(id, new SalidaRequest(null, null), Op);
        Assert.Equal(19000, salida.Pesada!.Neto);
        Assert.Equal(Op.Email, salida.Pesada.OperadorSalida);
        Assert.Equal("Fija", salida.Pesada.BalanzaSalida);
    }

    private sealed class BalanzaFija : IBalanzas
    {
        private LecturaBalanza _lectura = LecturaBalanza.Ninguna;

        public void Marcar(int kg, bool estable, string estado = "abierta") =>
            _lectura = new LecturaBalanza(Guid.NewGuid(), "Fija", kg, estable, estado, "COM1", DateTimeOffset.UtcNow);

        public IReadOnlyList<LecturaBalanza> Lecturas => [_lectura];
        public LecturaBalanza Lectura(Guid? balanzaId = null) => _lectura;
        public IReadOnlyList<string> UltimasTramas(Guid balanzaId) => [];
        public IReadOnlyList<string> Puertos() => [];
        public void Aplicar(IReadOnlyList<BalanzaConfig> balanzas) { }
    }
}
