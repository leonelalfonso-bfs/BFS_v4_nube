using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Documents;
using Bfs.Integrations.Worms;
using ClosedXML.Excel;

namespace Bfs.Tests;

/// <summary>Lo que pidió Worms Argentina: especificación de la API, campos, empresas, numeración, correcciones y Excel.</summary>
public class WormsTests
{
    private static readonly Guid Empresa1 = TestApi.Empresa1;

    private static Pesada Pesada(string tipo = "INGRESO") => new()
    {
        Id = Guid.NewGuid(), TicketNumero = "65327", Tipo = tipo, PatenteChasis = "AB123CD", Operador = "porteria1",
        Procedencia = "ROSARIO", Destino = "CAMPANA", PesoEntrada = 44240, PesoSalida = 15020,
        EntradaEn = new DateTimeOffset(2026, 10, 20, 17, 5, 30, TimeSpan.Zero), SalidaEn = new DateTimeOffset(2026, 10, 20, 17, 48, 10, TimeSpan.Zero),
        Aduana = new DatosAduana { Escenario = "Ingresa con Contenedor", ContenedorId = "MSCU1234567", ContenedorTara = 3800 }
    };

    [Fact]
    public void Payload_segun_la_especificacion()
    {
        var entrada = JsonDocument.Parse(JsonSerializer.Serialize(WormsMapper.FromPesada(Pesada(), WormsEventos.Entrada, "2", "Worms"), WormsOutbox.Json)).RootElement;

        Assert.Equal("65327", entrada.GetProperty("ticket_nro").GetString());
        Assert.Equal("Worms", entrada.GetProperty("empresa").GetString());
        Assert.Equal(44240, entrada.GetProperty("peso_entrada").GetInt32());
        Assert.Equal("ROSARIO", entrada.GetProperty("procedencia").GetString());
        Assert.Equal("MSCU1234567", entrada.GetProperty("contenedor_nro").GetString());
        Assert.Equal(3800, entrada.GetProperty("tara_contenedor").GetInt32());
        Assert.Matches(@"^2026-10-20T\d\d:05:30[+-]\d\d:\d\d$", entrada.GetProperty("entrada_en").GetString());
        // Lo que la balanza no registra se omite, no va como null.
        Assert.False(entrada.TryGetProperty("salida_en", out _));
        Assert.False(entrada.TryGetProperty("peso_salida", out _));
        Assert.False(entrada.TryGetProperty("cliente", out _));

        var salida = JsonDocument.Parse(JsonSerializer.Serialize(WormsMapper.FromPesada(Pesada("EGRESO"), WormsEventos.Salida, "2"), WormsOutbox.Json)).RootElement;
        Assert.Equal(15020, salida.GetProperty("peso_salida").GetInt32());
        Assert.Equal("CAMPANA", salida.GetProperty("procedencia").GetString()); // en un egreso va el destino
        Assert.False(salida.TryGetProperty("empresa", out _));
    }

    [Fact]
    public void Solo_las_empresas_elegidas_envian()
    {
        var otra = Guid.NewGuid();
        Dictionary<string, string> Config(string empresas) => new() { [ConfigKeys.WormsEnabled] = "true", [ConfigKeys.WormsEmpresas] = empresas };

        Assert.True(WormsOutbox.EmpresaEnvia(Config(""), otra));
        Assert.True(WormsOutbox.EmpresaEnvia(Config($"{Empresa1}"), Empresa1));
        Assert.False(WormsOutbox.EmpresaEnvia(Config($"{Empresa1}"), otra));
        Assert.False(WormsOutbox.EmpresaEnvia(Config("ninguna"), Empresa1));
        Assert.False(WormsOutbox.EmpresaEnvia(new Dictionary<string, string> { [ConfigKeys.WormsEnabled] = "false" }, Empresa1));
    }

    [Fact]
    public async Task Campos_obligatorios_numeracion_y_envio_por_empresa()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var empresas = await Json(admin.GetAsync("/api/empresas"));
        var interna = empresas.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).First(id => id != Empresa1);

        var campos = FieldCatalog.Defaults
            .Select(f => f with { Required = f.Required || f.Key is "producto" or "cliente" or "transporte" or "procedencia" or "conductor" })
            .ToList();
        await api.Configurar(new()
        {
            [ConfigKeys.FieldsJson] = JsonSerializer.Serialize(campos, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            [ConfigKeys.TicketSoloNumero] = "true",
            [ConfigKeys.WormsEnabled] = "true", [ConfigKeys.WormsEndpoint] = "http://127.0.0.1:9/worms", [ConfigKeys.WormsApiKey] = "k",
            [ConfigKeys.WormsEmpresas] = Empresa1.ToString()
        });

        // Los obligatorios los controla también el servidor, no solo la pantalla.
        var incompleta = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000 });
        Assert.Equal(HttpStatusCode.BadRequest, incompleta.StatusCode);
        Assert.Contains("Producto", await incompleta.Content.ReadAsStringAsync());

        // Sigue la numeración que traían: el próximo es el 78271.
        (await admin.PutAsJsonAsync("/api/sistema/tickets", new { empresaId = Empresa1, proximo = 78271 })).EnsureSuccessStatusCode();
        var a = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Completa(Empresa1)));
        var b = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Completa(Empresa1)));
        Assert.Equal("78271", a.GetProperty("ticketNumero").GetString());
        Assert.Equal("78272", b.GetProperty("ticketNumero").GetString());
        Assert.Equal("DNI", a.GetProperty("tipoDocumento").GetString());

        // No se puede volver a un número ya usado.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/sistema/tickets", new { empresaId = Empresa1, proximo = 78272 })).StatusCode);

        // La empresa de movimientos internos tiene su propia numeración y no le manda nada a WORMS.
        var internaPesada = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Completa(interna)));
        Assert.Equal("1", internaPesada.GetProperty("ticketNumero").GetString());

        using var vendor = await api.LoginVendor();
        var envios = await Json(vendor.GetAsync("/api/integraciones"));
        Assert.Equal(["78271", "78272"], envios.GetProperty("mensajes").EnumerateArray().Select(m => m.GetProperty("ticket").GetString()).Order());
    }

    [Fact]
    public async Task Aduana_guarda_ata_destino_y_lot()
    {
        await using var api = await TestApi.Iniciar();
        await api.Configurar(new() { [ConfigKeys.Aduana] = "true" });
        using var admin = await api.LoginAdmin();
        (await admin.PutAsJsonAsync("/api/planta/aduana", new { codigo = "123", lot = "LOT-77" })).EnsureSuccessStatusCode();

        var p = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000, tipoOperacion = "Aduana", nacionalidad = "Chilena", tipoDocumento = "Pasaporte",
            aduana = new { escenario = "Ingresa con Contenedor", ata = "ATA SRL", ataCuit = "20-12345678-6", destinacionAduanera = "EC01", procedenciaDestino = "Valparaíso", idDestino = "D-9" }
        }));
        var ad = p.GetProperty("aduana");
        Assert.Equal("ATA SRL", ad.GetProperty("ata").GetString());
        Assert.Equal("20-12345678-6", ad.GetProperty("ataCuit").GetString());
        Assert.Equal("EC01", ad.GetProperty("destinacionAduanera").GetString());
        Assert.Equal("Valparaíso", ad.GetProperty("procedenciaDestino").GetString());
        Assert.Equal("D-9", ad.GetProperty("idDestino").GetString());
        Assert.Equal("LOT-77", ad.GetProperty("lot").GetString());
        Assert.Equal("Chilena", ad.GetProperty("choferNacionalidad").GetString());
        Assert.Equal("Chilena", p.GetProperty("nacionalidad").GetString());
    }

    [Fact]
    public async Task Escenario_de_aduana_fijo()
    {
        await using var api = await TestApi.Iniciar();
        await api.Configurar(new() { [ConfigKeys.Aduana] = "true", [ConfigKeys.AduanaEscenario] = "Carga Contenedor Lleno" });
        using var admin = await api.LoginAdmin();

        // Sin escenario, o con otro, queda el que fijó BFS.
        var sinEscenario = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000, tipoOperacion = "Aduana" }));
        var otro = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", new
        {
            empresaId = Empresa1, patenteChasis = "AB123CE", pesoEntrada = 30000, tipoOperacion = "Aduana", aduana = new { escenario = "Ingresa con Contenedor" }
        }));
        Assert.Equal("Carga Contenedor Lleno", sinEscenario.GetProperty("aduana").GetProperty("escenario").GetString());
        Assert.Equal("Carga Contenedor Lleno", otro.GetProperty("aduana").GetProperty("escenario").GetString());
    }

    [Fact]
    public async Task Corregir_datos_con_clave_e_historial_sin_tocar_pesos()
    {
        await using var api = await TestApi.Iniciar();
        await api.Configurar(new()
        {
            [ConfigKeys.WormsEnabled] = "true", [ConfigKeys.WormsEndpoint] = "http://127.0.0.1:9/worms", [ConfigKeys.WormsApiKey] = "k",
            [ConfigKeys.WormsReenviarCorrecciones] = "true"
        });
        using var admin = await api.LoginAdmin();
        (await admin.PutAsJsonAsync("/api/planta/clave-correccion", new { clave = "1234" })).EnsureSuccessStatusCode();
        var p = await Json(admin.PostAsJsonAsync("/api/pesadas/entrada", Completa(Empresa1)));
        var id = p.GetProperty("id").GetGuid();
        await Json(admin.PostAsJsonAsync($"/api/pesadas/{id}/salida", new { pesoSalida = 12000 }));
        var corregir = $"/api/pesadas/{id}/corregir";

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(corregir, new { motivo = "x", clave = "mala", datos = new { cliente = "Otro" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(corregir, new { motivo = "", clave = "1234", datos = new { cliente = "Otro" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(corregir, new { motivo = "x", clave = "1234", datos = new { pesoEntrada = "1" } })).StatusCode);

        var c = await Json(admin.PostAsJsonAsync(corregir, new { motivo = "Cliente mal elegido", clave = "1234", datos = new { cliente = "Agro SA", patenteChasis = "ab 999 cd" } }));
        Assert.Equal("Agro SA", c.GetProperty("cliente").GetString());
        Assert.Equal("AB999CD", c.GetProperty("patenteChasis").GetString());
        Assert.Equal(30000, c.GetProperty("pesoEntrada").GetInt32());
        var historial = JsonDocument.Parse(c.GetProperty("correccionesJson").GetString()!).RootElement;
        var correccion = historial.EnumerateArray().Single();
        Assert.Equal("Cliente mal elegido", correccion.GetProperty("motivo").GetString());
        Assert.Equal(2, correccion.GetProperty("cambios").GetArrayLength());

        // El operador no tiene el permiso.
        await admin.PostAsJsonAsync("/api/usuarios", new { email = "op@planta.com", password = "clave123", nombre = "Op", rol = "operador" });
        using var op = await api.Login("op@planta.com", "clave123");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync(corregir, new { motivo = "x", clave = "1234", datos = new { cliente = "Otro" } })).StatusCode);

        // Con el reenvío habilitado, WORMS recibe otra vez ENTRADA y SALIDA con los datos nuevos.
        using var vendor = await api.LoginVendor();
        var envios = (await Json(vendor.GetAsync("/api/integraciones"))).GetProperty("mensajes").EnumerateArray().ToList();
        Assert.Equal(4, envios.Count);

        // El Excel lleva la corrección en su hoja.
        var excel = await admin.GetAsync("/api/pesadas/excel");
        using var wb = new XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync()));
        var hoja = wb.Worksheet("Correcciones");
        Assert.Equal("Cliente mal elegido", hoja.Cell(2, 5).GetString());
        Assert.Contains(wb.Worksheet("Pesadas").Row(3).CellsUsed(), x => x.GetString() == "ATA");
    }

    private static object Completa(Guid empresa) => new
    {
        empresaId = empresa, patenteChasis = "AB123CD", pesoEntrada = 30000, producto = "SEBO", cliente = "Proveedor SA",
        transporte = "Transporte SA", procedencia = "Rosario", conductor = "Juan Pérez", tipoDocumento = "DNI", documento = "20123456"
    };

    private static async Task<JsonElement> Json(Task<HttpResponseMessage> request)
    {
        var res = await request;
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }
}
