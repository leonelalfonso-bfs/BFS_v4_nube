using System.Security.Cryptography;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Importacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

/// <summary>Importación de una base de la 3.x armada acá, con columnas extra como las de un cliente real.</summary>
public class ImportadorTests : IAsyncLifetime
{
    private readonly TempDir _tmp = new();
    private BfsDbContext _db = null!;
    private string _vieja = null!;

    public async Task InitializeAsync()
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(_tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(NodoInfo.Ninguno));
        _db = new BfsDbContext(o.Options);
        await DbInitializer.SeedAsync(_db);
        _vieja = _tmp.File("truckscale.db");
        CrearBase3x(_vieja);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _tmp.Dispose();
    }

    private Task<ResultadoImportacion> Importar() =>
        new Importador3x(_db, new PasswordHasher<Usuario>(), "America/Argentina/Buenos_Aires", _tmp.File("branding"))
            .ImportarAsync(_vieja, TestApi.Empresa1);

    [Fact]
    public async Task Importa_todo_y_la_base_vieja_no_cambia()
    {
        var antes = Hash(_vieja);
        var r = await Importar();
        Assert.Equal(antes, Hash(_vieja));

        Assert.Equal(new ConteoImportacion(4, 0), r.Conteos["Pesadas"]);
        Assert.Equal(new ConteoImportacion(2, 0), r.Conteos["Clientes"]);
        Assert.Equal(new ConteoImportacion(3, 0), r.Conteos["Usuarios"]);

        var empresa = await _db.Empresas.SingleAsync(e => e.Id == TestApi.Empresa1);
        Assert.Equal("Acopio Sur SA", empresa.Nombre);
        Assert.Equal("20-12345678-6", empresa.Cuit);
        Assert.Equal("Chaco", empresa.Provincia);

        var config = await _db.Configuraciones.Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor);
        Assert.Equal("true", config[ConfigKeys.TaraConocida]);
        Assert.Equal("true", config[ConfigKeys.Aduana]);
        Assert.Equal("58", config[ConfigKeys.TermicaAncho]);
        Assert.Equal("017", config[ConfigKeys.AduanaCodigo]);
        Assert.Equal("2027-03-31", config[ConfigKeys.AduanaCertificado1Vence]);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<Usuario>().VerifyHashedPassword(new Usuario(), config[ConfigKeys.ClaveManualHash], "4321"));
        Assert.Contains(r.Avisos, a => a.Contains("logo"));
        Assert.Contains(r.Avisos, a => a.Contains("99-99999999-9"));
    }

    [Fact]
    public async Task Pesadas_con_estados_fechas_aduana_y_tara_conocida()
    {
        await Importar();
        var pesadas = await _db.Pesadas.ToDictionaryAsync(p => p.TicketNumero);

        var normal = pesadas["5"];
        Assert.Equal(EstadosPesada.Salida, normal.Estado);
        Assert.Equal(("AB123CD", 30000, 12000, 18000), (normal.PatenteChasis, normal.PesoEntrada, normal.PesoSalida!.Value, normal.Neto!.Value));
        Assert.Equal(new DateTimeOffset(2026, 8, 13, 13, 31, 15, TimeSpan.Zero), normal.EntradaEn);   // 10:31:15 en Argentina
        Assert.Equal("maxi", normal.Operador);
        Assert.NotNull(normal.ClienteId);
        Assert.Equal(500, normal.TaraPallets);

        var pendiente = pesadas["6"];
        Assert.Equal(EstadosPesada.Entrada, pendiente.Estado);
        Assert.Null(pendiente.PesoSalida);
        Assert.Equal(Importador3x.OperadorImportado, pendiente.Operador);

        var aduana = pesadas["7"];
        Assert.Equal(TiposOperacion.Aduana, aduana.TipoOperacion);
        Assert.Equal(("Carga Contenedor Lleno", "MSKU1", 2000, 16000), (aduana.Aduana!.Escenario, aduana.Aduana.ContenedorId, aduana.Aduana.ContenedorTara, aduana.Aduana.ContenedorNeto!.Value));
        Assert.Equal(ModosPesaje.TaraConocida, pesadas["8"].Modo);

        // Los tickets nuevos siguen después del último importado.
        Assert.Equal(9, (await _db.ContadoresTicket.SingleAsync(c => c.EmpresaId == TestApi.Empresa1 && c.Puesto == 1)).Proximo);
    }

    [Fact]
    public async Task Usuarios_roles_portal_y_balanza()
    {
        await Importar();
        var maxi = await _db.Usuarios.SingleAsync(u => u.Email == "maxi");
        Assert.Equal(Roles.Supervisor, maxi.Rol);
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<Usuario>().VerifyHashedPassword(maxi, maxi.PasswordHash, "clave-maxi"));

        // El usuario de portal queda atado a su cliente; si no estaba en el catálogo, se crea.
        var portal = await _db.Usuarios.SingleAsync(u => u.Email == "compras");
        Assert.Equal(Roles.Cliente, portal.Rol);
        Assert.Equal("Cliente Portal SRL", (await _db.Clientes.SingleAsync(c => c.Id == portal.ClienteId)).RazonSocial);

        // La "Balanza 1" simulada de la base nueva pasa a ser la real de la 3.x.
        var b = await _db.Balanzas.SingleAsync();
        Assert.Equal(("Balanza 1", ModosBalanza.Serie, "COM3", 4800, "4:10", ProtocolosBalanza.Line), (b.Nombre, b.Modo, b.Puerto, b.Baudios, b.Extraccion, b.Protocolo));

        var t = await _db.Transportes.SingleAsync(x => x.RazonSocial == "Trans ATA");
        Assert.True(t.EsAta);
        Assert.Equal("ATA-1", t.CodigoAta);
    }

    [Fact]
    public async Task Importar_dos_veces_no_duplica()
    {
        await Importar();
        // Aunque la empresa haya cambiado de nombre en la primera.
        var segunda = await Importar();
        Assert.Equal(new ConteoImportacion(0, 4), segunda.Conteos["Pesadas"]);
        Assert.Equal(new ConteoImportacion(0, 2), segunda.Conteos["Clientes"]);
        Assert.Equal(new ConteoImportacion(0, 3), segunda.Conteos["Usuarios"]);
        Assert.Equal(4, await _db.Pesadas.CountAsync());
    }

    [Fact]
    public async Task Un_archivo_que_no_es_de_la_3x_se_rechaza()
    {
        var otro = _tmp.File("otro.db");
        using (var c = new SqliteConnection($"Data Source={otro};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "CREATE TABLE cosas (id INTEGER)";
            cmd.ExecuteNonQuery();
        }
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Importador3x(_db, new PasswordHasher<Usuario>(), "UTC", _tmp.File("b")).ImportarAsync(otro, TestApi.Empresa1));
        Assert.Contains("weighings", ex.Message);
    }

    private static string Hash(string archivo) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivo)));

    /// <summary>Una base 3.x chica, con las columnas que fueron apareciendo (tara_pallets, usuario_entrada, tablas extra).</summary>
    private static void CrearBase3x(string archivo)
    {
        using var c = new SqliteConnection($"Data Source={archivo};Pooling=False");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE clients (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT, direccion TEXT);
            CREATE TABLE transports (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT, es_ata INTEGER, codigo_ata TEXT);
            CREATE TABLE productos (id INTEGER PRIMARY KEY, nombre TEXT UNIQUE, codigo TEXT);
            CREATE TABLE exportadores (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT);
            CREATE TABLE taras (carro_id TEXT PRIMARY KEY, tara REAL NOT NULL, descripcion TEXT, fecha TEXT NOT NULL);
            CREATE TABLE users (username TEXT PRIMARY KEY, password TEXT, privilege_level INTEGER, client_razon TEXT);
            CREATE TABLE balanzas (nombre TEXT PRIMARY KEY, puerto TEXT, baudrate TEXT, regex TEXT, databits INTEGER, parity TEXT, stopbits INTEGER, protocolo TEXT, modelo TEXT, decimales INTEGER);
            CREATE TABLE balanza_modelos (modelo TEXT PRIMARY KEY, baudrate TEXT, regex TEXT, databits INTEGER, parity TEXT, stopbits INTEGER, protocolo TEXT);
            CREATE TABLE client_config (id INTEGER PRIMARY KEY, razon_social TEXT, cuit TEXT, direccion TEXT, localidad TEXT, provincia TEXT, mail TEXT, logo_path TEXT,
              clave_manual TEXT, enable_tara_conocida INTEGER, enable_ingreso_manual INTEGER, telefono TEXT, enable_thermal_printer INTEGER, thermal_printer_name TEXT, thermal_paper_width TEXT);
            CREATE TABLE system_config (config_key TEXT PRIMARY KEY, config_value TEXT);
            CREATE TABLE aduana_config (id INTEGER PRIMARY KEY, codigo_aduana TEXT, codigo_lot TEXT, certificado_numero TEXT, certificado_vencimiento TEXT);
            CREATE TABLE weighing_data_cache (patente_chasis TEXT PRIMARY KEY, patente_acoplado TEXT, nombre_chofer TEXT, dni_chofer TEXT);
            CREATE TABLE tanques (id INTEGER PRIMARY KEY, nombre TEXT);
            CREATE TABLE weighings (id INTEGER PRIMARY KEY, ticket_numero TEXT, patente_chasis TEXT, patente_acoplado TEXT, nombre_chofer TEXT, dni_chofer TEXT,
              client_razon TEXT, transport_razon TEXT, procedencia TEXT, destino TEXT, remito TEXT, observaciones TEXT, peso_entrada REAL, peso_salida REAL,
              bruto REAL, tara REAL, neto REAL, fecha TEXT, fecha_salida TEXT, salida_pendiente INTEGER, tipo_operacion TEXT, producto TEXT,
              aduana_escenario TEXT, contenedor_identificador TEXT, contenedor_peso_tara REAL, contenedor_peso_neto REAL, contenedor_peso_bruto REAL,
              exportador_razon_social TEXT, foto_entrada TEXT, tara_pallets REAL, usuario_entrada TEXT, usuario_salida TEXT, precintos TEXT, carro_id TEXT);

            INSERT INTO clients VALUES (1, 'Agro SA', '20-12345678-6', NULL), (2, 'Molinos', '99-99999999-9', NULL);
            INSERT INTO transports VALUES (1, 'Trans ATA', NULL, 1, 'ATA-1'), (2, 'Propio', NULL, 0, NULL);
            INSERT INTO productos VALUES (1, 'Soja', '001');
            INSERT INTO exportadores VALUES (1, 'Export SA', NULL);
            INSERT INTO taras VALUES ('cc 333 cc', 14000, 'Batea', '01/08/2026 10:00:00');
            INSERT INTO users VALUES ('admin', 'clave-admin', 2, NULL), ('Maxi', 'clave-maxi', 1, NULL), ('compras', 'clave-portal', 0, 'Cliente Portal SRL');
            INSERT INTO balanzas VALUES ('Balanza 1', 'com3', '4800', '4:10', 8, 'None', 1, 'Line', NULL, 0);
            INSERT INTO balanza_modelos VALUES ('Moretti W-180', '9600', '5:10', 7, 'Even', 1, 'STX');
            INSERT INTO client_config VALUES (1, 'Acopio Sur SA', '20123456786', 'Ruta 11', 'Barranqueras', 'Chaco', 'a@acopio.com', 'C:\no\existe\logo.png',
              '4321', 1, 1, NULL, 1, 'POS-58', '58mm');
            INSERT INTO system_config VALUES ('operation_mode', 'Aduana'), ('datacloud_token', 'secreto');
            INSERT INTO aduana_config VALUES (1, '017', 'LOT', 'C-1', '31/03/2027');
            INSERT INTO weighing_data_cache VALUES ('AB123CD', NULL, 'Juan', '30111222');
            INSERT INTO weighings (id, ticket_numero, patente_chasis, client_razon, peso_entrada, peso_salida, bruto, tara, neto, fecha, fecha_salida,
              salida_pendiente, tipo_operacion, producto, tara_pallets, usuario_entrada, usuario_salida)
              VALUES (5, '5', 'ab 123 cd', 'agro sa', 30000.4, 12000, 30000, 12000, 18000, '13/08/2026 10:31:15', '13/08/2026 10:40:00', 0, 'Normal', 'Soja', 500, 'maxi', 'maxi');
            INSERT INTO weighings (id, ticket_numero, patente_chasis, peso_entrada, fecha, salida_pendiente, tipo_operacion)
              VALUES (6, '6', 'XX999XX', 25000, '14/08/2026 08:00:00', 1, 'Normal');
            INSERT INTO weighings (id, ticket_numero, patente_chasis, peso_entrada, peso_salida, bruto, tara, neto, fecha, fecha_salida, salida_pendiente,
              tipo_operacion, aduana_escenario, contenedor_identificador, contenedor_peso_tara, contenedor_peso_neto, contenedor_peso_bruto, exportador_razon_social, foto_entrada)
              VALUES (7, '7', 'DD444DD', 30000, 12000, 30000, 12000, 18000, '15/08/2026 09:00:00', '15/08/2026 09:30:00', 0,
              'Aduana', 'Carga Contenedor Lleno', 'MSKU1', 2000, 16000, 18000, 'Export SA', 'C:\fotos\7.jpg');
            INSERT INTO weighings (id, ticket_numero, patente_chasis, peso_entrada, peso_salida, neto, fecha, salida_pendiente, tipo_operacion)
              VALUES (8, '8', 'CC333CC', 40000, 14000, 26000, '16/08/2026 09:00:00', 0, 'Tara Conocida');
            """;
        cmd.ExecuteNonQuery();
    }
}
