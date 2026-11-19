using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Bfs.Api.Automatizacion;
using Bfs.Core;

namespace Bfs.Tests;

/// <summary>Automatización: placa Modbus (semáforos, barreras, sensores), secuencia automática y cámaras.</summary>
public class AutomatizacionTests
{
    /// <summary>Un esclavo Modbus TCP mínimo, para probar las tramas del cliente.</summary>
    private sealed class PlacaFalsa : IDisposable
    {
        private readonly TcpListener _escucha = new(IPAddress.Loopback, 0);
        public bool[] Entradas { get; } = [true, false, true, false, false, false, false, true];
        public bool[] Salidas { get; } = new bool[8];
        public int Puerto => ((IPEndPoint)_escucha.LocalEndpoint).Port;

        public PlacaFalsa()
        {
            _escucha.Start();
            _ = Task.Run(Atender);
        }

        private async Task Atender()
        {
            using var cliente = await _escucha.AcceptTcpClientAsync();
            var red = cliente.GetStream();
            var pedido = new byte[12];
            while (true)
            {
                try { await red.ReadExactlyAsync(pedido); }
                catch { return; }
                var funcion = pedido[7];
                var direccion = (pedido[8] << 8) | pedido[9];
                byte[] pdu;
                if (funcion is 1 or 2)
                {
                    var bits = funcion == 2 ? Entradas : Salidas;
                    byte valor = 0;
                    for (var i = 0; i < 8; i++) if (bits[i]) valor |= (byte)(1 << i);
                    pdu = [funcion, 1, valor];
                }
                else
                {
                    Salidas[direccion] = pedido[10] == 0xFF;
                    pdu = pedido[7..12];
                }
                byte[] respuesta = [pedido[0], pedido[1], 0, 0, 0, (byte)(pdu.Length + 1), pedido[6], .. pdu];
                await red.WriteAsync(respuesta);
            }
        }

        public void Dispose() => _escucha.Stop();
    }

    [Fact]
    public async Task El_cliente_modbus_lee_entradas_y_escribe_salidas()
    {
        using var falsa = new PlacaFalsa();
        using var placa = new PlacaModbus("127.0.0.1", falsa.Puerto, 1);
        await placa.ConectarAsync(CancellationToken.None);

        Assert.Equal(falsa.Entradas, await placa.LeerEntradasAsync(CancellationToken.None));
        await placa.EscribirSalidaAsync(3, true, CancellationToken.None);
        Assert.True(falsa.Salidas[2]);
        Assert.True((await placa.LeerSalidasAsync(CancellationToken.None))[2]);
        await placa.EscribirSalidaAsync(3, false, CancellationToken.None);
        Assert.False(falsa.Salidas[2]);
    }

    [Fact]
    public void La_configuracion_no_deja_dos_dispositivos_en_la_misma_salida()
    {
        var c = new ConfigAutomatizacion();
        Assert.Null(c.Validar());
        c.Dispositivos[1] = c.Dispositivos[1] with { Punto = 1 };
        Assert.Contains("salida 1", c.Validar());
    }

    [Fact]
    public async Task Apagada_no_cambia_nada()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var estado = await admin.GetFromJsonAsync<JsonElement>("/api/automatizacion/estado");
        Assert.False(estado.GetProperty("habilitada").GetBoolean());
        Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/funciones")).GetProperty("automatizacion").GetBoolean());
        var captura = await admin.PostAsJsonAsync("/api/pesadas/capturar", new { balanzaId = (Guid?)null });
        Assert.True(captura.IsSuccessStatusCode, await captura.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sensores_semaforos_secuencia_y_fotos()
    {
        await using var api = await TestApi.Iniciar();
        using var vendor = await api.LoginVendor();
        var config = new ConfigAutomatizacion
        {
            Habilitada = true,
            Simulada = true,
            SecuenciaAutomatica = true,
            Camaras = [new CamaraAutomatizacion("Cámara 1", "sim")]
        };
        var guardar = await vendor.PutAsJsonAsync("/api/automatizacion/config", config, ConfigAutomatizacion.Json);
        Assert.True(guardar.IsSuccessStatusCode, await guardar.Content.ReadAsStringAsync());
        await Esperar(vendor, e => e.GetProperty("conectada").GetBoolean());
        Assert.True((await vendor.GetFromJsonAsync<JsonElement>("/api/funciones")).GetProperty("automatizacion").GetBoolean());

        using var admin = await api.LoginAdmin();

        // Sensor de salida de la balanza 1 cortado (el camión pisa esa punta): no se captura.
        (await vendor.PostAsJsonAsync("/api/automatizacion/simular/2", new { valor = false })).EnsureSuccessStatusCode();
        var bloqueada = await admin.PostAsJsonAsync("/api/pesadas/capturar", new { balanzaId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, bloqueada.StatusCode);
        Assert.Contains("cortado", await bloqueada.Content.ReadAsStringAsync());

        // Camión entero arriba, ningún sensor cortado: se captura, con la foto de la cámara.
        (await vendor.PostAsJsonAsync("/api/automatizacion/simular/2", new { valor = true })).EnsureSuccessStatusCode();
        var captura = await (await admin.PostAsJsonAsync("/api/pesadas/capturar", new { balanzaId = (Guid?)null })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, captura.GetProperty("cantidadFotos").GetInt32());

        // Semáforos de la misma balanza: nunca dos en verde.
        await admin.PostAsJsonAsync("/api/automatizacion/salidas/1", new { valor = true });
        var estado = await (await admin.PostAsJsonAsync("/api/automatizacion/salidas/2", new { valor = true })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(estado.GetProperty("salidas")[0].GetBoolean());
        Assert.True(estado.GetProperty("salidas")[1].GetBoolean());
        await admin.PostAsJsonAsync("/api/automatizacion/salidas/1", new { valor = true });

        // Aceptar la entrada: foto guardada y secuencia (semáforo de salida en verde, barrera de salida abierta).
        var entrada = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", capturaId = captura.GetProperty("id").GetGuid() });
        Assert.True(entrada.IsSuccessStatusCode, await entrada.Content.ReadAsStringAsync());
        var pesada = await entrada.Content.ReadFromJsonAsync<JsonElement>();
        var fotos = JsonSerializer.Deserialize<List<FotoPesada>>(pesada.GetProperty("fotosJson").GetString()!, ConfigAutomatizacion.Json)!;
        Assert.Equal("entrada", Assert.Single(fotos).Momento);
        var id = pesada.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/pesadas/{id}/fotos/0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/pesadas/{id}/ticket")).StatusCode);

        estado = await admin.GetFromJsonAsync<JsonElement>("/api/automatizacion/estado");
        Assert.False(estado.GetProperty("salidas")[0].GetBoolean());
        Assert.True(estado.GetProperty("salidas")[1].GetBoolean());
        Assert.True(estado.GetProperty("salidas")[5].GetBoolean());

        // Con el camión todavía arriba (sensores en verde) la balanza no vuelve a la entrada.
        await Task.Delay(600);
        Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/automatizacion/estado")).GetProperty("salidas")[5].GetBoolean());

        // El camión sale: corta el sensor de salida mientras pasa y después todos vuelven a verde.
        await vendor.PostAsJsonAsync("/api/automatizacion/simular/2", new { valor = false });
        await Task.Delay(600);
        Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/automatizacion/estado")).GetProperty("salidas")[5].GetBoolean());
        await vendor.PostAsJsonAsync("/api/automatizacion/simular/2", new { valor = true });
        estado = await Esperar(admin, e => !e.GetProperty("salidas")[5].GetBoolean());
        Assert.True(estado.GetProperty("salidas")[0].GetBoolean());
        Assert.False(estado.GetProperty("salidas")[1].GetBoolean());

        // La cámara en vivo.
        var vivo = await admin.GetAsync("/api/automatizacion/camaras/0");
        Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);
        Assert.Equal("image/png", vivo.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<JsonElement> Esperar(HttpClient http, Func<JsonElement, bool> condicion)
    {
        JsonElement estado = default;
        for (var i = 0; i < 50; i++)
        {
            estado = await http.GetFromJsonAsync<JsonElement>("/api/automatizacion/estado");
            if (condicion(estado)) return estado;
            await Task.Delay(100);
        }
        Assert.Fail("No llegó al estado esperado: " + estado);
        return estado;
    }
}
