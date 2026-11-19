using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Importacion;
using Bfs.Integrations.Centum;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

/// <summary>Edición Inagro: campos de su 3.x, numeración compartida, Centum, remito preimpreso e importación.</summary>
public class InagroTests
{
    private const string Cuit = "20-12345678-6";

    [Fact]
    public void Edicion_inagro_tiene_su_carpeta_puerto_y_reglas()
    {
        Assert.Equal(Edicion.Inagro, Ediciones.Efectiva(Edicion.Completa, Edicion.Inagro));
        Assert.Equal(Edicion.Inagro, Ediciones.Efectiva(Edicion.Inagro, Edicion.Completa));
        Assert.Equal(Edicion.Estandar, Ediciones.Efectiva(Edicion.Inagro, Edicion.Estandar));
        Assert.Equal(Edicion.Estandar, Ediciones.Efectiva(Edicion.Estandar, Edicion.Inagro));
        Assert.Equal("bfs-4-inagro", Ediciones.CarpetaDatos(Edicion.Inagro));
        Assert.Equal(3, new[] { Edicion.Completa, Edicion.Estandar, Edicion.Inagro }.Select(Ediciones.UrlLocal).Distinct().Count());
        Assert.True(Ediciones.TieneIntegraciones(Edicion.Inagro));
        Assert.True(Ediciones.NumeracionCompartida(Edicion.Inagro));
        Assert.False(Ediciones.NumeracionCompartida(Edicion.Completa));
    }

    [Theory]
    [InlineData("00011-20609", "00011-00020609")]
    [InlineData("4-17404", "00004-00017404")]
    [InlineData("0000417404", "00000-00417404")]
    [InlineData("", "00000-00000000")]
    [InlineData("123456-123456789", "23456-23456789")]
    public void Remito_con_el_formato_de_centum(string entrada, string esperado) =>
        Assert.Equal(esperado, Inagro.Remito(entrada));

    [Fact]
    public void Centum_recibe_lo_mismo_que_mandaba_la_3x()
    {
        var p = new Pesada
        {
            TicketNumero = "01-000010",
            PatenteChasis = "NWZ336",
            PatenteAcoplado = "AB996FZ",
            Cliente = "PATAGONIA",
            Comprobante = "00011-20609",
            Observaciones = "ARRIOLA con un texto bastante largo",
            Documento = "28667781",
            Neto = 28460,
            Inagro = new DatosInagro
            {
                Movimiento = MovimientosInagro.Compras,
                PesoOrigen = 28460,
                Tanque = "TK 12",
                Analisis = "70863",
                ProductoCodigo = "a-0001",
                Planta = "1: Alvear",
                ClienteCuit = "30-70996794-7",
                TransporteCuit = "30-59262026-6"
            }
        };
        var d = CentumMapper.FromPesada(p, "2", new DateTime(2026, 11, 19, 3, 34, 44));

        Assert.Equal(2, d["tipo_sociedad_id"]);
        Assert.Equal(1, d["tipo_movimiento_id"]);
        Assert.Equal("00011-00020609", d["numero_remito"]);
        Assert.Equal("70863", d["numero_analisis"]);
        Assert.Equal("19/11/2026", d["fecha_entrada"]);
        Assert.Equal("03:34:44", d["hora_entrada"]);
        Assert.Equal("0NWZ336-AB996FZ", d["patente_tractor"]);
        Assert.Equal("30709967947", d["proveedor"]);
        Assert.Equal("PATAGONIA", d["razon_proveedor"]);
        Assert.False(d.ContainsKey("cliente"));
        Assert.Equal("00028,46", d["cant_un_nivel"]);
        Assert.Equal("30592620266", d["transporte_cuit"]);
        Assert.Equal("28667781", d["chofer_dni"]);
        Assert.Equal("A0001", d["producto_codigo"]);
        Assert.Equal("00028,46", d["peso_neto_kg"]);
        Assert.Equal("TK 12", d["seccion"]);
        Assert.Equal("ARRIOLA con un texto", d["observaciones"]);
        Assert.Equal("1", d["planta"]);
        Assert.False(d.ContainsKey("suc_desde"));

        // Venta: va como cliente. OT: sin proveedor, con sucursales desde / hacia y sin peso de origen si no lo tiene.
        p.Inagro.Movimiento = MovimientosInagro.Ventas;
        var venta = CentumMapper.FromPesada(p, "1", DateTime.Now);
        Assert.Equal("30709967947", venta["cuit_cliente"]);
        Assert.False(venta.ContainsKey("proveedor"));

        p.Inagro.Movimiento = MovimientosInagro.Traspaso;
        p.Inagro.PesoOrigen = null;
        p.Procedencia = "ALVEAR";
        p.Destino = "CAMPANA";
        var ot = CentumMapper.FromPesada(p, "1", DateTime.Now);
        Assert.Equal("ALVEAR", ot["suc_desde"]);
        Assert.Equal("CAMPANA", ot["suc_hacia"]);
        Assert.False(ot.ContainsKey("cant_un_nivel"));
    }

    [Fact]
    public void Remito_preimpreso_se_genera_con_textos_largos()
    {
        var p = new Pesada
        {
            TicketNumero = "01-000001", PatenteChasis = "NWZ336", Cliente = "PATAGONIA", Producto = "Borras Vegetales", Bruto = 43800, Tara = 15340, Neto = 28460,
            Observaciones = string.Join(" ", Enumerable.Repeat("observación larga", 40)),
            Inagro = new DatosInagro { Analisis = "70863", Tanque = "TK 12", ObservacionesRemito = string.Join(" ", Enumerable.Repeat("remito", 60)), ClienteCuit = Cuit }
        };
        var t = new Bfs.Documents.TicketDatos(p, new Bfs.Documents.EmpresaTicket("Inagro S.A.", null, null, null, null, null, null), "", null, null,
            new DateTime(2026, 11, 19, 3, 34, 44), null, null, null, null, null);
        var pdf = Bfs.Documents.RemitoPreimpreso.Generar(t, 1.5f, -2);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf[..4]));
    }

    [Fact]
    public async Task Pesaje_inagro_completo()
    {
        await using var api = await TestApi.Iniciar(extra: "--Bfs:Edicion=Inagro");
        using var anon = api.Cliente();
        Assert.Equal("Inagro", (await anon.GetFromJsonAsync<JsonElement>("/api/health")).GetProperty("edicion").GetString());
        await api.Configurar(new() { [ConfigKeys.CentumEnabled] = "true", [ConfigKeys.CentumEndpoint] = "http://127.0.0.1:9/centum", [ConfigKeys.InagroPlanta] = "2: Campana" });

        using var vendor = await api.LoginVendor();
        // Una instalación nueva arranca con las sociedades de Centum como empresas.
        var empresas = (await vendor.GetFromJsonAsync<JsonElement>("/api/empresas")).EnumerateArray().ToList();
        Assert.Equal(4, empresas.Count);
        Assert.Equal("1", empresas.Single(e => e.GetProperty("id").GetGuid() == TestApi.Empresa1).GetProperty("codigoSociedad").GetString());
        var noVincula = empresas.Single(e => e.GetProperty("codigoSociedad").GetString() == Inagro.SociedadNoVincula).GetProperty("id").GetGuid();
        var srl = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // Catálogos de INAGRO y datos para el remito.
        (await vendor.PostAsJsonAsync("/api/catalogos/tanques", new { nombre = "TK 12" })).EnsureSuccessStatusCode();
        (await vendor.PostAsJsonAsync("/api/catalogos/sucursales", new { nombre = "ALVEAR" })).EnsureSuccessStatusCode();
        (await vendor.PostAsJsonAsync("/api/catalogos/clientes", new { razonSocial = "PATAGONIA", cuit = Cuit, direccion = "Colectora A012", localidad = "SAN LORENZO", condicionIva = "Responsable Inscripto" })).EnsureSuccessStatusCode();
        (await vendor.PostAsJsonAsync("/api/catalogos/transportes", new { razonSocial = "Inagro SRL", cuit = Cuit, direccion = "Av. Perón 8725", localidad = "PARQUE LELOIR" })).EnsureSuccessStatusCode();
        (await vendor.PostAsJsonAsync("/api/catalogos/productos", new { nombre = "Borras Vegetales", codigo = "A0001" })).EnsureSuccessStatusCode();
        // Editar el cliente sin mandar el domicilio (como la pantalla de otra edición) no lo borra.
        var cliente = (await vendor.GetFromJsonAsync<JsonElement>("/api/catalogos/clientes?q=PATAGONIA"))[0];
        (await vendor.PutAsJsonAsync($"/api/catalogos/clientes/{cliente.GetProperty("id").GetGuid()}", new { razonSocial = "PATAGONIA", cuit = Cuit })).EnsureSuccessStatusCode();

        using var admin = await api.LoginAdmin();
        object Entrada(Guid empresa, object? inagro) => new
        {
            empresaId = empresa, patenteChasis = "NWZ336", patenteAcoplado = "AB996FZ", cliente = "PATAGONIA", transporte = "Inagro SRL",
            producto = "Borras Vegetales", procedencia = "SAN LORENZO", destino = "ALVEAR", comprobante = "00011-20609", observaciones = "ARRIOLA",
            documento = "28667781", pesoEntrada = 43800, inagro
        };

        // Sin Id de operación no entra.
        var sinMov = await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(TestApi.Empresa1, null));
        Assert.Equal(HttpStatusCode.BadRequest, sinMov.StatusCode);
        var analisisMal = await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(TestApi.Empresa1, new { movimiento = 1, analisis = "12AB" }));
        Assert.Equal(HttpStatusCode.BadRequest, analisisMal.StatusCode);

        var r1 = await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(TestApi.Empresa1,
            new { movimiento = 1, pesoOrigen = 28460, tanque = "TK 12", analisis = "70863", precintos = "P-1", cuilChofer = "20-28667781-4", observacionesRemito = "KG DE BORRAS" }));
        Assert.True(r1.IsSuccessStatusCode, await r1.Content.ReadAsStringAsync());
        var p1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var i1 = p1.GetProperty("inagro");
        Assert.Equal("01-000001", p1.GetProperty("ticketNumero").GetString());
        Assert.Equal("INGRESO", p1.GetProperty("tipo").GetString());
        Assert.Equal(28460, i1.GetProperty("pesoOrigen").GetInt32());
        Assert.Equal("A0001", i1.GetProperty("productoCodigo").GetString());
        Assert.Equal("20286677814", i1.GetProperty("cuilChofer").GetString());
        Assert.Equal("2: Campana", i1.GetProperty("planta").GetString());
        Assert.Equal("Colectora A012", i1.GetProperty("clienteDireccion").GetString());
        Assert.Equal("Responsable Inscripto", i1.GetProperty("clienteCondicionIva").GetString());
        Assert.Equal("PARQUE LELOIR", i1.GetProperty("transporteLocalidad").GetString());

        // Otra sociedad sigue la misma numeración. Ventas = egreso y sin peso de origen.
        var r2 = await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(srl, new { movimiento = 2, pesoOrigen = 999 }));
        var p2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("01-000002", p2.GetProperty("ticketNumero").GetString());
        Assert.Equal("EGRESO", p2.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, p2.GetProperty("inagro").GetProperty("pesoOrigen").ValueKind);

        var r3 = await admin.PostAsJsonAsync("/api/pesadas/entrada", Entrada(noVincula, new { movimiento = 3 }));
        var p3 = await r3.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("01-000003", p3.GetProperty("ticketNumero").GetString());

        // Sistema → Numeración muestra un solo contador.
        var sistema = await vendor.GetFromJsonAsync<JsonElement>("/api/sistema");
        var tickets = sistema.GetProperty("tickets");
        Assert.Equal(1, tickets.GetArrayLength());
        Assert.Equal(4, tickets[0].GetProperty("proximo").GetInt32());

        // Al cerrar se encola para Centum; la empresa "No vincula" no.
        foreach (var p in new[] { p1, p3 })
        {
            var salida = await admin.PostAsJsonAsync($"/api/pesadas/{p.GetProperty("id").GetGuid()}/salida", new { pesoSalida = 15340 });
            Assert.True(salida.IsSuccessStatusCode, await salida.Content.ReadAsStringAsync());
        }
        var outbox = LeerOutbox(api.DataPath);
        var centum = Assert.Single(outbox, o => o.Destino == CentumDestino.Destino);
        Assert.Equal("01-000001", centum.Ticket);
        var payload = JsonDocument.Parse(centum.Payload).RootElement;
        Assert.Equal(1, payload.GetProperty("tipo_sociedad_id").GetInt32());
        Assert.Equal("00028,46", payload.GetProperty("peso_neto_kg").GetString());
        Assert.Equal("00028,46", payload.GetProperty("cant_un_nivel").GetString());
        Assert.Equal("2", payload.GetProperty("planta").GetString());
        Assert.Equal("20286677814", payload.GetProperty("chofer_dni").GetString());

        // Ticket y remito preimpreso.
        var id1 = p1.GetProperty("id").GetGuid();
        var remito = await admin.GetAsync($"/api/pesadas/{id1}/remito");
        Assert.Equal(HttpStatusCode.OK, remito.StatusCode);
        Assert.Equal("application/pdf", remito.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString((await remito.Content.ReadAsByteArrayAsync())[..4]));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/pesadas/{id1}/ticket")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/pesadas/excel")).StatusCode);

        // Corrección de un campo de INAGRO, con historial.
        var corr = await admin.PostAsJsonAsync($"/api/pesadas/{id1}/corregir", new { motivo = "Tanque equivocado", datos = new Dictionary<string, string?> { ["inagro.tanque"] = "TK 306", ["inagro.analisis"] = "70864" } });
        Assert.True(corr.IsSuccessStatusCode, await corr.Content.ReadAsStringAsync());
        Assert.Equal("TK 306", (await corr.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("inagro").GetProperty("tanque").GetString());
        var malCorr = await admin.PostAsJsonAsync($"/api/pesadas/{id1}/corregir", new { motivo = "x", datos = new Dictionary<string, string?> { ["inagro.movimiento"] = "7" } });
        Assert.Equal(HttpStatusCode.BadRequest, malCorr.StatusCode);
    }

    [Fact]
    public async Task La_completa_no_tiene_nada_de_inagro()
    {
        await using var api = await TestApi.Iniciar();
        using var admin = await api.LoginAdmin();
        var r = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = TestApi.Empresa1, patenteChasis = "AB123CD", pesoEntrada = 30000, inagro = new { movimiento = 1, tanque = "TK" } });
        var p = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, p.GetProperty("inagro").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/pesadas/{p.GetProperty("id").GetGuid()}/remito")).StatusCode);

        var e2 = await admin.PostAsJsonAsync("/api/pesadas/entrada", new { empresaId = Guid.Parse("22222222-2222-2222-2222-222222222222"), patenteChasis = "AB123CE", pesoEntrada = 30000 });
        // Cada empresa con su numeración.
        Assert.Equal("01-000001", (await e2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticketNumero").GetString());
    }

    [Fact]
    public async Task Importa_una_base_de_inagro()
    {
        using var tmp = new TempDir();
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(NodoInfo.Ninguno));
        await using var db = new BfsDbContext(o.Options);
        await DbInitializer.SeedAsync(db, datosDePrueba: true, sociedades: Inagro.SociedadesIniciales);
        var vieja = tmp.File("truckscale.db");
        CrearBaseInagro(vieja);

        var r = await new Importador3x(db, new PasswordHasher<Usuario>(), "America/Argentina/Buenos_Aires", tmp.File("branding"))
            .ImportarAsync(vieja, TestApi.Empresa1);

        Assert.Equal(new ConteoImportacion(3, 0), r.Conteos["Pesadas"]);
        Assert.Equal(new ConteoImportacion(2, 0), r.Conteos["Tanques"]);
        Assert.Equal(new ConteoImportacion(1, 0), r.Conteos["Sucursales"]);
        Assert.Equal("Colectora A012", (await db.Clientes.SingleAsync(c => c.RazonSocial == "PATAGONIA")).Direccion);
        Assert.Equal("2: Campana", (await db.Configuraciones.SingleAsync(c => c.Clave == ConfigKeys.InagroPlanta)).Valor);

        var pesadas = await db.Pesadas.ToDictionaryAsync(p => p.TicketNumero);
        var compra = pesadas["8352"];
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), compra.EmpresaId);
        Assert.Equal(MovimientosInagro.Compras, compra.Inagro!.Movimiento);
        Assert.Equal(28460, compra.Inagro.PesoOrigen);
        Assert.Equal("TK 12", compra.Inagro.Tanque);
        Assert.Equal("70863", compra.Inagro.Analisis);
        Assert.Equal("A0001", compra.Inagro.ProductoCodigo);
        Assert.Equal("30-70996794-7", compra.Inagro.ClienteCuit);
        Assert.Equal("Responsable Inscripto", compra.Inagro.ClienteCondicionIva);
        Assert.Equal("2: Campana", compra.Inagro.Planta);
        Assert.Equal("INGRESO", compra.Tipo);

        var venta = pesadas["8353"];
        Assert.Equal("EGRESO", venta.Tipo);
        Assert.Null(venta.Inagro!.PesoOrigen);
        var noVincula = await db.Empresas.SingleAsync(e => e.CodigoSociedad == Inagro.SociedadNoVincula);
        Assert.Equal(noVincula.Id, pesadas["8354"].EmpresaId);

        // La numeración compartida sigue después del último ticket de la 3.x.
        var contador = await db.ContadoresTicket.SingleAsync(c => c.EmpresaId == ContadoresCompartidos.Empresa && c.Puesto == 1);
        Assert.Equal(8355, contador.Proximo);
    }

    private static void CrearBaseInagro(string archivo)
    {
        using var c = new SqliteConnection($"Data Source={archivo};Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE clients (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT, direccion TEXT, localidad TEXT, condicion_iva TEXT);
            CREATE TABLE transports (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT, es_ata INTEGER, codigo_ata TEXT, direccion TEXT, localidad TEXT, condicion_iva TEXT);
            CREATE TABLE tanques (id INTEGER PRIMARY KEY, nombre TEXT UNIQUE);
            CREATE TABLE sucursales (id INTEGER PRIMARY KEY, nombre TEXT UNIQUE);
            CREATE TABLE system_config (config_key TEXT PRIMARY KEY, config_value TEXT);
            CREATE TABLE weighings (id INTEGER PRIMARY KEY, ticket_numero TEXT, patente_chasis TEXT, patente_acoplado TEXT, nombre_chofer TEXT, dni_chofer TEXT,
              client_razon TEXT, client_cuit TEXT, client_direccion TEXT, client_localidad TEXT, client_condicion_iva TEXT,
              transport_razon TEXT, transport_cuit TEXT, procedencia TEXT, destino TEXT, remito TEXT, observaciones TEXT,
              peso_entrada REAL, peso_salida REAL, bruto REAL, tara REAL, neto REAL, fecha TEXT, fecha_salida TEXT, salida_pendiente INTEGER,
              tipo_operacion TEXT, producto TEXT, producto_codigo TEXT, tanque TEXT, numero_analisis TEXT, precintos TEXT, cuil_chofer TEXT,
              observaciones_remito TEXT, peso_origen REAL, tipo_movimiento INTEGER, tipo_sociedad_id INTEGER, usuario_entrada TEXT, usuario_salida TEXT);

            INSERT INTO clients VALUES (1, 'PATAGONIA', '30-70996794-7', 'Colectora A012', 'SAN LORENZO', 'Responsable Inscripto');
            INSERT INTO transports VALUES (1, 'Inagro SRL', '30-59262026-6', 0, NULL, 'Av. Peron 8725', 'PARQUE LELOIR', 'Responsable Inscripto');
            INSERT INTO tanques (nombre) VALUES ('TK 12'), ('TK 306');
            INSERT INTO sucursales (nombre) VALUES ('ALVEAR');
            INSERT INTO system_config VALUES ('planta_instalacion', '2: Campana');
            INSERT INTO weighings VALUES (1, '8352', 'NWZ336', 'AB996FZ', 'PEREZ NELSON', '28-667781', 'PATAGONIA', '30-70996794-7', 'Colectora A012', 'SAN LORENZO',
              'Responsable Inscripto', 'Inagro SRL', '30-59262026-6', 'SAN LORENZO', 'ALVEAR', '00011-20609', 'ARRIOLA', 43800.0, 15340.0, 43800.0, 15340.0, 28460.0,
              '19/11/2026 03:34:44', '19/11/2026 08:39:32', 0, 'Normal', 'Borras Vegetales', 'A0001', 'TK 12', '70863', '', NULL, '', 28460.0, 1, 2, 'admin1', 'admin1');
            INSERT INTO weighings VALUES (2, '8353', 'KZE880', NULL, NULL, NULL, 'PATAGONIA', '30-70996794-7', NULL, NULL, NULL, NULL, NULL, 'ALVEAR', 'Worms', NULL, NULL,
              15000.0, 0.0, 0.0, 0.0, 0.0, '19/11/2026 07:54:39', NULL, 1, 'Normal', 'Efluentes', 'A0044', 'TK 306', NULL, NULL, NULL, NULL, 500.0, 2, 1, 'admin1', NULL);
            INSERT INTO weighings VALUES (3, '8354', 'OIG256', NULL, NULL, NULL, 'PATAGONIA', NULL, NULL, NULL, NULL, NULL, NULL, 'ALVEAR', 'CAMPANA', NULL, NULL,
              14400.0, 0.0, 0.0, 0.0, 0.0, '19/11/2026 07:48:41', NULL, 1, 'Normal', 'Efluentes', NULL, NULL, NULL, NULL, NULL, NULL, NULL, 3, 4, 'admin1', NULL);
            """;
        cmd.ExecuteNonQuery();
    }

    private record FilaOutbox(string Destino, string Ticket, string Payload);

    private static List<FilaOutbox> LeerOutbox(string dataPath)
    {
        using var conn = new SqliteConnection($"Data Source={Path.Combine(dataPath, "bfs.db")};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Destino, TicketNumero, PayloadJson FROM Outbox";
        using var r = cmd.ExecuteReader();
        var lista = new List<FilaOutbox>();
        while (r.Read()) lista.Add(new FilaOutbox(r.GetString(0), r.GetString(1), r.GetString(2)));
        return lista;
    }
}
