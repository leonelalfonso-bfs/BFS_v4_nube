using System.Security.Cryptography;
using System.Text;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Importacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Tests;

/// <summary>Migración directa desde un dump MySQL (<c>.sql</c>) de la versión 1.x (<c>sist_pesada</c>) hacia BFS 4.0.</summary>
public class Importador1xTests : IAsyncLifetime
{
    private readonly TempDir _tmp = new();
    private BfsDbContext _db = null!;
    private string _dumpSql = null!;

    public async Task InitializeAsync()
    {
        var o = new DbContextOptionsBuilder<BfsDbContext>();
        BfsDatabase.Configurar(o, ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(_tmp.File("bfs.db")));
        o.AddInterceptors(new SyncStampInterceptor(NodoInfo.Ninguno));
        _db = new BfsDbContext(o.Options);
        await DbInitializer.SeedAsync(_db);
        _dumpSql = _tmp.File("backup_v1.sql");
        CrearDump1x(_dumpSql);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        _tmp.Dispose();
    }

    private Task<ResultadoImportacion> Importar() =>
        new Importador1x(_db, new PasswordHasher<Usuario>(), "America/Argentina/Buenos_Aires", _tmp.File("branding"))
            .ImportarAsync(_dumpSql, TestApi.Empresa1);

    [Fact]
    public async Task Importa_empresa_catalogos_usuarios_balanza_y_no_toca_el_sql()
    {
        Assert.True(Importador1x.EsBase1x(_dumpSql));
        var conteoPrevio = Importador1x.Contar(_dumpSql);
        Assert.Equal(5, conteoPrevio["pesadas"]);
        Assert.Equal(3, conteoPrevio["clientes"]);

        var hashAntes = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(_dumpSql)));
        var r = await Importar();
        var hashDespues = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(_dumpSql)));
        Assert.Equal(hashAntes, hashDespues);

        // 3 filas en clientes pero 2 tenían el mismo nombre ("CASTELAR"), así que se unifican sin perder el FK.
        Assert.Equal(new ConteoImportacion(2, 1), r.Conteos["Clientes"]);
        Assert.Equal(new ConteoImportacion(5, 0), r.Conteos["Pesadas"]);
        Assert.Equal(new ConteoImportacion(3, 0), r.Conteos["Usuarios"]);

        var empresa = await _db.Empresas.SingleAsync(e => e.Id == TestApi.Empresa1);
        Assert.Equal("AGRO ACEITUNERA SA - AGRO 4", empresa.Nombre);
        Assert.Equal("30-59629816-4", empresa.Cuit);
        Assert.Equal("AIMOGASTA", empresa.Localidad);
        Assert.Equal("LA RIOJA", empresa.Provincia);

        var config = await _db.Configuraciones.Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor);
        Assert.Equal("true", config[ConfigKeys.Aduana]);
        Assert.Equal("true", config[ConfigKeys.Contenedores]);
        Assert.Equal("079 RES2023-5-EAFIP", config[ConfigKeys.AduanaCodigo]);
        Assert.Equal("5300H", config[ConfigKeys.AduanaLot]);
        Assert.Equal("307-54366 VPE87007", config[ConfigKeys.AduanaCertificado1]);
        Assert.Equal("2026-11-27", config[ConfigKeys.AduanaCertificado1Vence]);
        Assert.Equal("", config[ConfigKeys.LogoFile]); // El PNG por defecto de BFS no pisa el logo del cliente.
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<Usuario>().VerifyHashedPassword(new Usuario(), config[ConfigKeys.ClaveManualHash], "FRASCA"));

        // admin de la 1.x pasa a admin-1x; supervisor y operador mantienen su clave.
        Assert.True(await _db.Usuarios.AnyAsync(u => u.Email == "admin-1x" && u.Rol == Roles.Admin));
        var sup = await _db.Usuarios.SingleAsync(u => u.Email == "frasca");
        Assert.Equal(Roles.Supervisor, sup.Rol);
        var op = await _db.Usuarios.SingleAsync(u => u.Email == "margarita");
        Assert.Equal(Roles.Operador, op.Rol);

        // La balanza simulada inicial pasa a ser el indicador serie de la 1.x.
        var bal = await _db.Balanzas.SingleAsync();
        Assert.Equal(("AGRO 4", ModosBalanza.Serie, "COM1", 9600, 8, "None", 1),
            (bal.Nombre, bal.Modo, bal.Puerto, bal.Baudios, bal.DataBits, bal.Paridad, bal.StopBits));

        // El ATA queda marcado en Transportes con EsAta = true.
        var ata = await _db.Transportes.SingleAsync(t => t.RazonSocial == "ATA RIOJA SRL");
        Assert.True(ata.EsAta);
    }

    [Fact]
    public async Task Pesadas_normales_aduana_pendientes_tickets_repetidos_y_autocompletado()
    {
        var r = await Importar();
        var pesadas = await _db.Pesadas.ToDictionaryAsync(p => p.TicketNumero);

        // Egreso con contenedor lleno (entra vacío 19560, sale cargado 43600 -> neto 24040, contenedor tara 3700 -> neto cont 20340).
        var aduana = pesadas["100"];
        Assert.Equal(TiposOperacion.Aduana, aduana.TipoOperacion);
        Assert.Equal("EGRESO", aduana.Tipo);
        Assert.Equal(EstadosPesada.Salida, aduana.Estado);
        Assert.Equal((43600, 19560, 24040), (aduana.Bruto!.Value, aduana.Tara!.Value, aduana.Neto!.Value));
        Assert.Equal(EscenariosAduana.CargaContenedorLleno, aduana.Aduana!.Escenario);
        Assert.Equal(("HAMU483791-5", 3700, 24040, 20340),
            (aduana.Aduana.ContenedorId, aduana.Aduana.ContenedorTara, aduana.Aduana.ContenedorBruto!.Value, aduana.Aduana.ContenedorNeto!.Value));
        Assert.Equal("AGRO EXPORT SA", aduana.Aduana.Exportador);
        Assert.Equal("ATA RIOJA SRL", aduana.Aduana.Ata);
        Assert.Equal("25015663", aduana.Aduana.OperacionId);

        // Cliente duplicado (codigo 7 -> mismo "CASTELAR" que codigo 4) conserva Cliente y ClienteId.
        var normal = pesadas["101"];
        Assert.Equal("INGRESO", normal.Tipo);
        Assert.Equal("CASTELAR", normal.Cliente);
        Assert.NotNull(normal.ClienteId);
        Assert.Equal("AIMOGASTA", normal.Procedencia);

        // Número de transacción repetido en la 1.x en distinta fecha se conserva como "101-2".
        Assert.True(pesadas.ContainsKey("101-2"));
        Assert.Contains(r.Avisos, a => a.Contains("repetido"));

        // Pesada abierta (fecha_salida NULL) queda en ENTRADA pendiente de salida.
        var pendiente = pesadas["105"];
        Assert.Equal(EstadosPesada.Entrada, pendiente.Estado);
        Assert.Null(pendiente.PesoSalida);

        // EMPRESA_TRANSACCION era 27271 -> próximo ticket 27272.
        Assert.Equal(27272, (await _db.ContadoresTicket.SingleAsync(c => c.EmpresaId == TestApi.Empresa1 && c.Puesto == 1)).Proximo);
        Assert.Equal(27272, (await _db.Empresas.SingleAsync(e => e.Id == TestApi.Empresa1)).ProximoTicket);

        // Caché de patentes poblada desde el historial para autocompletar en Pesaje.
        var veh = await _db.DatosVehiculos.SingleAsync(v => v.PatenteChasis == "AB123CD");
        Assert.Equal("JUAN PEREZ", veh.Conductor);
        Assert.Equal("AF999ZZ", veh.PatenteAcoplado);
    }

    [Fact]
    public async Task Correr_dos_veces_es_idempotente_incluso_con_tickets_renumerados()
    {
        await Importar();
        var segunda = await Importar();

        Assert.Equal(new ConteoImportacion(0, 5), segunda.Conteos["Pesadas"]);
        Assert.Equal(new ConteoImportacion(0, 3), segunda.Conteos["Clientes"]);
        Assert.Equal(new ConteoImportacion(0, 3), segunda.Conteos["Usuarios"]);
        Assert.Equal(5, await _db.Pesadas.CountAsync());
    }

    [Fact]
    public async Task Endpoint_importar_acepta_backup_sql_subido_por_formulario()
    {
        await using var api = await TestApi.Iniciar();
        using var vendor = await api.LoginVendor();

        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(_dumpSql);
        form.Add(new ByteArrayContent(bytes), "file", "backup_27422026_1142.sql");

        var resp = await vendor.PostAsync("/api/sistema/importar-3x", form);
        resp.EnsureSuccessStatusCode();

        var json = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<System.Text.Json.JsonElement>(resp.Content);
        Assert.Equal("1.x", json.GetProperty("version").GetString());
        Assert.Equal(5, json.GetProperty("conteos").GetProperty("Pesadas").GetProperty("importados").GetInt32());
    }

    private static void CrearDump1x(string ruta)
    {
        var sql = """
            -- MySQL dump 10.13  Distrib 5.5.62, for Win64 (AMD64)
            -- Host: localhost    Database: sist_pesada
            /*!40101 SET NAMES utf8 */;

            CREATE TABLE `parametros_globales` (
              `id` varchar(45) NOT NULL,
              `value` varchar(150) DEFAULT NULL,
              `valueByte` longblob,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `parametros_globales` VALUES
              ('ACTIVAR_DEBUG','false','\x89PNG\r\n\Z\nFAKEBFS'),
              ('EMPRESA_IMG','','\x89PNG\r\n\Z\nFAKEBFS'),
              ('EMPRESA_NOMBRE','BALANZAS FULL SERVICE SRL',NULL),
              ('EMPRESA_NOMBRE_BAL','AGRO ACEITUNERA SA - AGRO 4 - CUIT:30-59629816-4',NULL),
              ('EMPRESA_DIR_BAL','ESPECTACION DE AVILA S/N',NULL),
              ('EMPRESA_LOC_BAL','AIMOGASTA',NULL),
              ('EMPRESA_PROV_BAL','LA RIOJA',NULL),
              ('EMPRESA_TEL_BAL','RESOL2023-5-EAFIP',NULL),
              ('EMPRESA_ING_MANUAL','FRASCA','\x89PNG\r\n\Z\nFAKEBFS'),
              ('CODIGO_ADUANA','079 RES2023-5-EAFIP',NULL),
              ('CODIGO_LOG','5300H',NULL),
              ('CERTIFICADO','307-54366 VPE87007',NULL),
              ('VENCIMIENTO','27/11/2026',NULL),
              ('EMPRESA_TRANSACCION','27271',NULL),
              ('TICKET_ETIQUETADORA','false',NULL);

            CREATE TABLE `clientes` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              `CUIT` varchar(20) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `clientes` VALUES (4,'CASTELAR','30-50000001-2'),(7,'CASTELAR',''),(10,'OLIVARES SRL','30-71534221-5');

            CREATE TABLE `transportes` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              `CUIT` varchar(20) DEFAULT NULL,
              `ultimo_movimiento` datetime DEFAULT NULL,
              `acumulado` decimal(12,3) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `transportes` VALUES (3,'TRANS NORTE','30-71534221-5',NULL,0.000);

            CREATE TABLE `ata` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              `CUIT` varchar(20) DEFAULT NULL,
              `ultimo_movimiento` datetime DEFAULT NULL,
              `acumulado` decimal(12,3) DEFAULT NULL,
              `nacionalidad` varchar(45) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `ata` VALUES (5,'ATA RIOJA SRL','30-71534221-5',NULL,0.000,'ARGENTINA');

            CREATE TABLE `productos` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              `alias` varchar(45) DEFAULT NULL,
              `ultimo_movimiento` datetime DEFAULT NULL,
              `acumulado` decimal(12,3) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `productos` VALUES (1,'ACEITUNA EN SALMUERA','ACEIT',NULL,0.000);

            CREATE TABLE `importadores_exportadores` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              `CUIT` varchar(20) DEFAULT NULL,
              `ultimo_movimiento` datetime DEFAULT NULL,
              `acumulado` decimal(12,3) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `importadores_exportadores` VALUES (9,'AGRO EXPORT SA','30-59629816-4',NULL,0.000);

            CREATE TABLE `procedencias` (
              `codigo` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(100) DEFAULT NULL,
              PRIMARY KEY (`codigo`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `procedencias` VALUES (2,'AIMOGASTA');

            CREATE TABLE `indicadores` (
              `idindicadores` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(45) DEFAULT NULL,
              `posicion_caracter_control` int(11) DEFAULT NULL,
              `longitud_caracter_control` int(11) DEFAULT NULL,
              `caracter_control` varchar(10) DEFAULT NULL,
              `posicion_inicio_dato` int(11) DEFAULT NULL,
              `longitud_dato` int(11) DEFAULT NULL,
              `puerto` int(11) DEFAULT NULL,
              `velocidad` int(11) DEFAULT NULL,
              `bits_de_datos` int(11) DEFAULT NULL,
              `paridad` varchar(10) DEFAULT NULL,
              `control_de_flujo` varchar(30) DEFAULT NULL,
              `bits_parada` varchar(10) DEFAULT NULL,
              PRIMARY KEY (`idindicadores`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `indicadores` VALUES (9,'AGRO 4',1,1,'',1,5,1,9600,8,'n','0 - comNone','1');

            CREATE TABLE `comunicaciones` (
              `idcomunicaciones` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(45) DEFAULT NULL,
              `idindicadores` int(11) DEFAULT NULL,
              PRIMARY KEY (`idcomunicaciones`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `comunicaciones` VALUES (1,'INDICADOR 1',9);

            CREATE TABLE `usuarios` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `nombre` varchar(45) DEFAULT NULL,
              `clave` varchar(45) DEFAULT NULL,
              `id_perfil` int(11) DEFAULT NULL,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `usuarios` VALUES (1,'admin','123456',1),(2,'FRASCA','FRASCA',2),(3,'MARGARITA ','MARGARITA',3);

            CREATE TABLE `taras` (
              `idtaras` int(11) NOT NULL AUTO_INCREMENT,
              `transaccion` varchar(45) DEFAULT NULL,
              `fecha_entrada` datetime DEFAULT NULL,
              `fecha_salida` datetime DEFAULT NULL,
              `balanza` varchar(45) DEFAULT NULL,
              `id_producto` int(11) DEFAULT NULL,
              `id_cliente` int(11) DEFAULT NULL,
              `id_transporte` int(11) DEFAULT NULL,
              `id_procedencia` int(11) DEFAULT NULL,
              `id_imp_exp` int(11) DEFAULT NULL,
              `modalidad` varchar(45) DEFAULT NULL,
              `comprobante_nun1` varchar(45) DEFAULT NULL,
              `modoTara` varchar(45) DEFAULT NULL,
              `destino` varchar(100) DEFAULT NULL,
              `conductor` varchar(100) DEFAULT NULL,
              `tipo_doc` varchar(20) DEFAULT NULL,
              `num_doc` varchar(30) DEFAULT NULL,
              `patente` varchar(20) DEFAULT NULL,
              `patente_aceptado` varchar(20) DEFAULT NULL,
              `observacion` varchar(255) DEFAULT NULL,
              `observacion_aduana` varchar(255) DEFAULT NULL,
              `contenedor_num` varchar(45) DEFAULT NULL,
              `peso_entrada` decimal(9,3) DEFAULT NULL,
              `peso_salida` decimal(9,3) DEFAULT NULL,
              `update_date` datetime DEFAULT NULL,
              `modoChasis` varchar(45) DEFAULT NULL,
              `id_ata` int(11) DEFAULT NULL,
              `contenedor` varchar(45) DEFAULT NULL,
              `manifiesto` varchar(45) DEFAULT NULL,
              `mercaderia` varchar(100) DEFAULT NULL,
              `nacionalidad` varchar(45) DEFAULT NULL,
              PRIMARY KEY (`idtaras`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8;

            INSERT INTO `taras` VALUES
              (1,'100','2026-11-26 09:00:00','2026-11-26 10:30:00','AGRO 4',1,10,3,2,9,'ADUANA','0001-000123','NORMAL','CHILE','JUAN PEREZ','DNI','30111222','ab 123 cd','af 999 zz','PRECINTO 1','26079EC01000123A','3700',19560.000,43600.000,'2026-11-26 10:30:00','COMPLETO',5,'HAMU483791-5','25015663','ACEITUNA','ARGENTINO'),
              (2,'101','2026-11-26 11:00:00','2026-11-26 11:45:00','AGRO 4',1,7,3,2,NULL,'ESTANDAR','R-55','NORMAL',NULL,'MARIO GOMEZ',NULL,'25000111','AA111AA',NULL,'SIN OBS',NULL,NULL,42000.000,15000.000,'2026-11-26 11:45:00','COMPLETO',NULL,NULL,NULL,NULL,'ARGENTINO'),
              (3,'101','2026-11-26 15:00:00','2026-11-26 16:00:00','AGRO 4',1,4,3,2,NULL,'ESTANDAR','','NORMAL',NULL,'LUIS DIAZ',NULL,'24000222','BB222BB',NULL,'OTRO CAMION MISMO TICKET',NULL,NULL,16000.000,44000.000,'2026-11-26 16:00:00','COMPLETO',NULL,NULL,NULL,NULL,'ARGENTINO'),
              (4,'102','2026-11-26 17:00:00','2026-11-26 17:30:00','AGRO 4',1,10,3,2,9,'ADUANA','','NORMAL','BRASIL','CARLOS SOSA','DNI','20999888','CC333CC','DD444DD','','','3800',44000.000,19000.000,'2026-11-26 17:30:00','COMPLETO',5,'MSKU123456-7','998877','ACEITUNA','ARGENTINO'),
              (5,'105','2026-11-27 11:03:33',NULL,'AGRO 4',1,10,3,2,NULL,'ESTANDAR','','NORMAL',NULL,'PEDRO LUNA',NULL,'33444555','AE464YT',NULL,'PENDIENTE HOY',NULL,NULL,52740.000,NULL,'2026-11-27 11:03:33','COMPLETO',NULL,NULL,NULL,NULL,'ARGENTINO');
            """;

        // Reemplaza \x89 literal en el bloque de prueba por el byte 0x89 real de cabecera PNG para probar el binario embebido.
        var bytes = Encoding.UTF8.GetBytes(sql.Replace("\\x89PNG", "\u0001PNG"));
        for (var i = 0; i < bytes.Length - 3; i++)
        {
            if (bytes[i] == 0x01 && bytes[i + 1] == (byte)'P' && bytes[i + 2] == (byte)'N' && bytes[i + 3] == (byte)'G')
                bytes[i] = 0x89;
        }
        File.WriteAllBytes(ruta, bytes);
    }
}
