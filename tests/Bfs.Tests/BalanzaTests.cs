using System.IO.Pipelines;
using System.Text;
using Bfs.Core;
using Bfs.Scale;
using Bfs.Scale.Tramas;

namespace Bfs.Tests;

/// <summary>
/// Sin tramas reales de clientes todavía: estas imitan los formatos de los modelos de la 3.x.
/// Cuando haya capturas de campo conviene sumarlas acá tal cual.
/// </summary>
public class BalanzaTests
{
    [Theory]
    [InlineData("ST,GS,012480,kg", "6:12", 12480)]
    [InlineData("ST,GS,-01250,kg", "6:12", -1250)]
    [InlineData("  12480 kg", null, 12480)]
    [InlineData("  12480 kg", "", 12480)]
    [InlineData("+0012480", "1:20", 12480)]      // fin más allá del largo: se corta en el largo, como la 3.x
    [InlineData("P 0012.6", "2:8", 13)]          // decimales: se redondea
    [InlineData("   -  250", null, -250)]        // signo separado por espacios
    public void Extrae_el_peso_como_la_3x(string trama, string? extraccion, int esperado)
    {
        Assert.Equal(esperado, ExtractorPeso.Extraer(trama, extraccion));
    }

    [Theory]
    [InlineData("ABC")]
    [InlineData("")]
    [InlineData(null)]
    public void Sin_numero_no_hay_peso(string? trama) => Assert.Null(ExtractorPeso.Extraer(trama, null));

    [Fact]
    public void Si_el_rango_no_tiene_digitos_usa_la_trama_limpia()
    {
        // Ruido de control al principio corre la trama: en crudo el rango 0:5 cae sobre basura, limpio cae sobre el número.
        Assert.Equal(12480, ExtractorPeso.Extraer("\u0001\u0001\u0001\u0001\u000112480", "0:5"));
    }

    [Theory]
    [InlineData("5:10", true)]
    [InlineData(" 1 : 7 ", true)]
    [InlineData("10:5", false)]
    [InlineData("a:b", false)]
    [InlineData("5", false)]
    public void Valida_el_rango(string extraccion, bool valido) => Assert.Equal(valido, ExtractorPeso.Rango(extraccion) != null);

    [Fact]
    public void Arma_tramas_stx_aunque_lleguen_cortadas()
    {
        var e = new EnsambladorTramas(ProtocolosBalanza.Stx);
        Assert.Empty(e.Agregar(Bytes("basura\x02 +012")));
        Assert.Equal([" +012480kg"], e.Agregar(Bytes("480kg\x03\x02 +0124")));
        Assert.Equal([" +012490kg"], e.Agregar(Bytes("90kg\x03")));
        Assert.Empty(e.Agregar(Bytes("\x03")));   // ETX sin STX no arma nada
    }

    [Fact]
    public void En_stx_sin_stx_procesa_cada_tanto_como_la_3x()
    {
        var e = new EnsambladorTramas(ProtocolosBalanza.Stx);
        var tramas = e.Agregar(Bytes(new string('1', EnsambladorTramas.LargoSinStx + 1)));
        Assert.Single(tramas);
    }

    [Fact]
    public void Arma_tramas_por_linea_con_cr_lf_o_crlf()
    {
        var e = new EnsambladorTramas(ProtocolosBalanza.Line);
        Assert.Equal(["  12480", "  12490", "  12500"], e.Agregar(Bytes("  12480\r\n  12490\n\n  12500\r")));
        Assert.Empty(e.Agregar(Bytes("  125")));
        Assert.Equal(["  12510"], e.Agregar(Bytes("10\r\n")));
    }

    [Fact]
    public void Una_trama_sin_fin_no_crece_para_siempre()
    {
        var e = new EnsambladorTramas(ProtocolosBalanza.Line);
        // Lo que sigue a una línea demasiado larga es parte de esa misma línea: se descarta hasta el salto.
        e.Agregar(Bytes(new string('x', EnsambladorTramas.LargoMaximo + 50)));
        Assert.Empty(e.Agregar(Bytes("12480\n")));
        Assert.Equal(["12490"], e.Agregar(Bytes("12490\n")));
    }

    [Fact]
    public void Estable_cuando_no_se_mueve_durante_el_tiempo()
    {
        var t0 = DateTimeOffset.UnixEpoch;
        var d = new DetectorEstabilidad(TimeSpan.FromSeconds(1), toleranciaKg: 10);
        Assert.False(d.Registrar(12480, t0));
        Assert.False(d.Registrar(12485, t0.AddMilliseconds(600)));
        Assert.True(d.Registrar(12475, t0.AddMilliseconds(1000)));
        Assert.False(d.Registrar(12600, t0.AddMilliseconds(1200)));   // se movió: arranca de nuevo
        Assert.True(d.Registrar(12600, t0.AddMilliseconds(2200)));
    }

    [Fact]
    public async Task Lee_por_puerto_y_publica_el_peso()
    {
        var puertos = new PuertosFalsos();
        using var manager = new BalanzaManager(puertos, reintento: TimeSpan.FromMilliseconds(50));
        var config = Config("Moretti", ProtocolosBalanza.Stx, "6:12");
        manager.Aplicar([config]);

        var pipe = await puertos.EsperarApertura();
        await Escribir(pipe, "\x02ST,GS,012480,kg\x03");
        await Esperar(() => manager.Lectura().Kilogramos == 12480);

        var lectura = manager.Lectura(config.Id);
        Assert.Equal("abierta", lectura.Estado);
        Assert.Equal("Moretti", lectura.Nombre);
        Assert.Equal(["ST,GS,012480,kg"], manager.UltimasTramas(config.Id));
    }

    [Fact]
    public async Task Si_no_abre_informa_el_error_y_reintenta_solo()
    {
        var puertos = new PuertosFalsos { FallosAntesDeAbrir = 2 };
        using var manager = new BalanzaManager(puertos, reintento: TimeSpan.FromMilliseconds(50));
        manager.Aplicar([Config("B1", ProtocolosBalanza.Line, null)]);

        await Esperar(() => manager.Lectura().Estado == "error");
        Assert.Contains("ocupado", manager.Lectura().Error);

        var pipe = await puertos.EsperarApertura();
        await Escribir(pipe, "  15000\r\n");
        await Esperar(() => manager.Lectura() is { Estado: "abierta", Kilogramos: 15000 });
        Assert.Equal(3, puertos.Intentos);
    }

    [Fact]
    public async Task Si_se_corta_el_cable_vuelve_a_conectar()
    {
        var puertos = new PuertosFalsos();
        using var manager = new BalanzaManager(puertos, reintento: TimeSpan.FromMilliseconds(50));
        manager.Aplicar([Config("B1", ProtocolosBalanza.Line, null)]);

        var pipe = await puertos.EsperarApertura();
        await pipe.Writer.CompleteAsync();   // el puerto se cerró del otro lado
        await Esperar(() => puertos.Intentos >= 2);

        var nuevo = await puertos.EsperarApertura();
        await Escribir(nuevo, "  9000\n");
        await Esperar(() => manager.Lectura().Kilogramos == 9000);
    }

    [Fact]
    public async Task Puerto_abierto_sin_datos_es_sin_senal()
    {
        var puertos = new PuertosFalsos();
        using var manager = new BalanzaManager(puertos, sinSenal: TimeSpan.FromMilliseconds(100));
        manager.Aplicar([Config("B1", ProtocolosBalanza.Line, null)]);
        await puertos.EsperarApertura();
        await Esperar(() => manager.Lectura().Estado == "sin señal");
    }

    [Fact]
    public async Task Varias_balanzas_a_la_vez()
    {
        var puertos = new PuertosFalsos();
        using var manager = new BalanzaManager(puertos);
        var b1 = Config("Entrada", ProtocolosBalanza.Line, null, "COM3");
        var b2 = Config("Salida", ProtocolosBalanza.Line, null, "COM4");
        manager.Aplicar([b1, b2]);

        await Escribir(await puertos.EsperarApertura("COM3"), "1000\n");
        await Escribir(await puertos.EsperarApertura("COM4"), "2000\n");
        await Esperar(() => manager.Lectura(b1.Id).Kilogramos == 1000 && manager.Lectura(b2.Id).Kilogramos == 2000);

        Assert.Equal(["Entrada", "Salida"], manager.Lecturas.Select(l => l.Nombre));
        Assert.Equal(1000, manager.Lectura().Kilogramos);   // sin indicar, la primera
    }

    [Fact]
    public async Task La_simulada_no_necesita_puerto_y_se_estabiliza()
    {
        var puertos = new PuertosFalsos();
        using var manager = new BalanzaManager(puertos);
        manager.Aplicar([Config("SIM", ProtocolosBalanza.Line, null) with { Modo = ModosBalanza.Simulada }]);
        await Esperar(() => manager.Lectura() is { Estado: "simulada", Kilogramos: > 0 });
        Assert.Equal(0, puertos.Intentos);
    }

    [Fact]
    public void Sin_balanzas_la_lectura_lo_dice()
    {
        Assert.Equal("sin balanza", new SinBalanzas().Lectura().Estado);
        using var manager = new BalanzaManager(new PuertosFalsos());
        Assert.Equal("sin balanza", manager.Lectura().Estado);
    }

    private static BalanzaConfig Config(string nombre, string protocolo, string? extraccion, string puerto = "COM3") =>
        new(Guid.NewGuid(), nombre, ModosBalanza.Serie, puerto, 9600, 8, "None", 1, protocolo, extraccion);

    private static byte[] Bytes(string s) => Encoding.Latin1.GetBytes(s);

    private static async Task Escribir(Pipe pipe, string datos)
    {
        await pipe.Writer.WriteAsync(Bytes(datos));
        await pipe.Writer.FlushAsync();
    }

    private static async Task Esperar(Func<bool> condicion, int ms = 3000)
    {
        var hasta = DateTime.UtcNow.AddMilliseconds(ms);
        while (!condicion())
        {
            if (DateTime.UtcNow > hasta) Assert.Fail("No se cumplió la condición a tiempo");
            await Task.Delay(20);
        }
    }

    /// <summary>Puertos de mentira: cada apertura entrega un pipe donde el test escribe lo que "manda el indicador".</summary>
    private sealed class PuertosFalsos : IPuertoSerieFactory
    {
        private readonly object _lock = new();
        private readonly List<(string Puerto, Pipe Pipe)> _abiertos = [];
        public int FallosAntesDeAbrir { get; set; }
        public int Intentos { get; private set; }

        public IReadOnlyList<string> Puertos() => ["COM3", "COM4"];

        public Stream Abrir(BalanzaConfig config)
        {
            lock (_lock)
            {
                Intentos++;
                if (FallosAntesDeAbrir-- > 0) throw new UnauthorizedAccessException($"El puerto {config.Puerto} está ocupado");
                var pipe = new Pipe();
                _abiertos.Add((config.Puerto, pipe));
                return pipe.Reader.AsStream();
            }
        }

        public async Task<Pipe> EsperarApertura(string? puerto = null)
        {
            var hasta = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < hasta)
            {
                lock (_lock)
                {
                    var i = _abiertos.FindIndex(a => puerto == null || a.Puerto == puerto);
                    if (i >= 0)
                    {
                        var pipe = _abiertos[i].Pipe;
                        _abiertos.RemoveAt(i);
                        return pipe;
                    }
                }
                await Task.Delay(10);
            }
            throw new TimeoutException("No se abrió el puerto");
        }
    }
}
