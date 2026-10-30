using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations;
using Bfs.Integrations.Worms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bfs.Tests;

public class IntegracionesTests
{
    private const string Endpoint = "https://worms.test/api/pesadas";

    /// <summary>WORMS de mentira: responde lo que diga la lista (y anota lo que le llegó).</summary>
    private sealed class WormsFalso : HttpMessageHandler
    {
        public Queue<Func<HttpResponseMessage>> Respuestas { get; } = new();
        public List<(string Evento, string? Clave)> Recibidos { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Recibidos.Add((json.RootElement.GetProperty("evento").GetString()!,
                request.Headers.TryGetValues("x-api-key", out var v) ? v.Single() : null));
            return Respuestas.Count > 0 ? Respuestas.Dequeue()() : new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class Fabrica(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private static async Task<BfsDbContext> Base(TempDir tmp, bool habilitado = true)
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(new NodoInfo(Guid.NewGuid())));
        var db = new BfsDbContext(o.Options);
        await DbInitializer.SeedAsync(db);
        var valores = new Dictionary<string, string>
        {
            [ConfigKeys.WormsEnabled] = habilitado ? "true" : "false",
            [ConfigKeys.WormsEndpoint] = Endpoint,
            [ConfigKeys.WormsApiKey] = "clave-worms"
        };
        foreach (var c in await db.Configuraciones.Where(c => c.EmpresaId == null && valores.Keys.Contains(c.Clave)).ToListAsync())
        {
            c.Valor = valores[c.Clave];
        }
        await db.SaveChangesAsync();
        return db;
    }

    private static OutboxMessage Encolar(BfsDbContext db, Guid pesada, string evento, DateTimeOffset creado, string ticket = "01-000001")
    {
        var m = new OutboxMessage
        {
            Id = Guid.NewGuid(), Destino = WormsDestino.Destino, PesadaId = pesada, TicketNumero = ticket, Evento = evento,
            PayloadJson = JsonSerializer.Serialize(new { evento }), CreadoEn = creado, ProximoIntento = creado
        };
        db.Outbox.Add(m);
        return m;
    }

    private static DespachadorOutbox Despachador(WormsFalso worms, TimeProvider reloj) =>
        new([new WormsDestino()], new Fabrica(worms), NullLogger<DespachadorOutbox>.Instance, reloj);

    [Fact]
    public void La_espera_crece_hasta_30_minutos()
    {
        Assert.Equal([1, 2, 4, 8, 16, 30, 30], new[] { 1, 2, 3, 4, 5, 6, 20 }.Select(i => DespachadorOutbox.Espera(i).TotalMinutes));
    }

    [Fact]
    public async Task Manda_con_la_clave_y_marca_enviado()
    {
        using var tmp = new TempDir();
        await using var db = await Base(tmp);
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var worms = new WormsFalso();
        var m = Encolar(db, Guid.NewGuid(), "ENTRADA", reloj.Ahora);
        worms.Respuestas.Enqueue(() => new HttpResponseMessage(HttpStatusCode.Conflict)); // ya lo tenía: cuenta como enviado
        await db.SaveChangesAsync();

        Assert.Equal(1, await Despachador(worms, reloj).ProcesarAsync(db));

        Assert.Equal([("ENTRADA", "clave-worms")], worms.Recibidos);
        await db.Entry(m).ReloadAsync();
        Assert.Equal(EstadosOutbox.Enviado, m.Estado);
        Assert.Equal(1, m.Intentos);
    }

    [Fact]
    public async Task Apagado_no_manda_nada()
    {
        using var tmp = new TempDir();
        await using var db = await Base(tmp, habilitado: false);
        var worms = new WormsFalso();
        Encolar(db, Guid.NewGuid(), "ENTRADA", DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();

        Assert.Equal(0, await Despachador(worms, TimeProvider.System).ProcesarAsync(db));
        Assert.Empty(worms.Recibidos);
    }

    [Fact]
    public async Task Caido_reintenta_con_espera_creciente_y_la_salida_espera_a_la_entrada()
    {
        using var tmp = new TempDir();
        await using var db = await Base(tmp);
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var worms = new WormsFalso();
        var pesada = Guid.NewGuid();
        var entrada = Encolar(db, pesada, "ENTRADA", reloj.Ahora.AddSeconds(-2));
        var salida = Encolar(db, pesada, "SALIDA", reloj.Ahora.AddSeconds(-1));
        await db.SaveChangesAsync();
        var despachador = Despachador(worms, reloj);

        worms.Respuestas.Enqueue(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        await despachador.ProcesarAsync(db);
        Assert.Equal(["ENTRADA"], worms.Recibidos.Select(r => r.Evento)); // la salida no se adelantó
        Assert.Equal(reloj.Ahora.AddMinutes(1), entrada.ProximoIntento);
        Assert.Contains("503", entrada.UltimoError);

        // Antes de que pase la espera no se intenta de nuevo.
        reloj.Ahora = reloj.Ahora.AddSeconds(30);
        await despachador.ProcesarAsync(db);
        Assert.Single(worms.Recibidos);

        // Sin conexión: segundo intento fallido, espera 2 minutos.
        reloj.Ahora = reloj.Ahora.AddMinutes(1);
        worms.Respuestas.Enqueue(() => throw new HttpRequestException("No se puede conectar"));
        await despachador.ProcesarAsync(db);
        Assert.Equal(2, entrada.Intentos);
        Assert.Equal(reloj.Ahora.AddMinutes(2), entrada.ProximoIntento);

        // Vuelve WORMS: salen la entrada y después la salida.
        reloj.Ahora = reloj.Ahora.AddMinutes(2);
        await despachador.ProcesarAsync(db);
        await despachador.ProcesarAsync(db);
        Assert.Equal(["ENTRADA", "ENTRADA", "ENTRADA", "SALIDA"], worms.Recibidos.Select(r => r.Evento));
        await db.Entry(salida).ReloadAsync();
        Assert.Equal(EstadosOutbox.Enviado, entrada.Estado);
        Assert.Equal(EstadosOutbox.Enviado, salida.Estado);
    }

    [Fact]
    public async Task Un_rechazo_queda_en_error_y_traba_solo_esa_pesada()
    {
        using var tmp = new TempDir();
        await using var db = await Base(tmp);
        var reloj = new Reloj(DateTimeOffset.UtcNow);
        var worms = new WormsFalso();
        var pesadaMala = Guid.NewGuid();
        var mala = Encolar(db, pesadaMala, "ENTRADA", reloj.Ahora.AddSeconds(-3), "01-000001");
        var salidaMala = Encolar(db, pesadaMala, "SALIDA", reloj.Ahora.AddSeconds(-2), "01-000001");
        var otra = Encolar(db, Guid.NewGuid(), "ENTRADA", reloj.Ahora.AddSeconds(-1), "01-000002");
        await db.SaveChangesAsync();
        worms.Respuestas.Enqueue(() => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("patente inválida") });

        var despachador = Despachador(worms, reloj);
        await despachador.ProcesarAsync(db);
        reloj.Ahora = reloj.Ahora.AddHours(1);
        await despachador.ProcesarAsync(db);

        Assert.Equal(EstadosOutbox.Error, mala.Estado);
        Assert.Equal("400: patente inválida", mala.UltimoError);
        Assert.Equal(EstadosOutbox.Pendiente, salidaMala.Estado);
        Assert.Equal(0, salidaMala.Intentos);
        Assert.Equal(EstadosOutbox.Enviado, otra.Estado);

        // Descartada la entrada rechazada, la salida de esa pesada ya puede salir.
        mala.Estado = EstadosOutbox.Descartado;
        await db.SaveChangesAsync();
        await despachador.ProcesarAsync(db);
        Assert.Equal(EstadosOutbox.Enviado, salidaMala.Estado);
    }

    [Fact]
    public async Task Pantalla_de_envios_muestra_reintenta_y_descarta()
    {
        await using var api = await TestApi.Iniciar();
        // Un puerto donde no escucha nadie: WORMS "caído".
        await api.Configurar(new()
        {
            [ConfigKeys.WormsEnabled] = "true", [ConfigKeys.WormsEndpoint] = "http://127.0.0.1:9/worms", [ConfigKeys.WormsApiKey] = "k"
        });
        using var admin = await api.LoginAdmin();
        var res = await admin.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000
        });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());

        using var vendor = await api.LoginVendor();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/integraciones")).StatusCode);

        var estado = await Estado(vendor.PostAsync("/api/integraciones/enviar", null));
        var worms = estado.GetProperty("destinos").EnumerateArray().Single(d => d.GetProperty("nombre").GetString() == "worms");
        Assert.True(worms.GetProperty("habilitado").GetBoolean());
        Assert.Equal(1, worms.GetProperty("pendientes").GetInt32());
        var m = estado.GetProperty("mensajes").EnumerateArray().Single();
        Assert.Equal("ENTRADA", m.GetProperty("evento").GetString());
        Assert.Equal(1, m.GetProperty("intentos").GetInt32());
        Assert.False(string.IsNullOrEmpty(m.GetProperty("ultimoError").GetString()));
        var id = m.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await vendor.PostAsync($"/api/integraciones/{id}/reintentar", null)).StatusCode);
        Assert.Equal(0, (await Estado(vendor.GetAsync("/api/integraciones"))).GetProperty("mensajes")[0].GetProperty("intentos").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await vendor.PostAsync($"/api/integraciones/{id}/descartar", null)).StatusCode);
        var descartados = await Estado(vendor.GetAsync("/api/integraciones?estado=descartado"));
        Assert.Equal(id, descartados.GetProperty("mensajes").EnumerateArray().Single().GetProperty("id").GetGuid());
        Assert.Empty((await Estado(vendor.GetAsync("/api/integraciones?estado=pendiente"))).GetProperty("mensajes").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await vendor.PostAsync($"/api/integraciones/{Guid.NewGuid()}/reintentar", null)).StatusCode);
    }

    private static async Task<JsonElement> Estado(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }
}
