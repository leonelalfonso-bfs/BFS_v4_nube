using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Infrastructure.Importacion;

/// <summary>
/// Pasa un dump MySQL (<c>.sql</c>) de la versión 1.x (<c>sist_pesada</c>) directo a la 4.0 sin pasar por la 3.x.
/// Lee el archivo en binario (porque <c>parametros_globales.valueByte</c> trae PNGs crudos adentro del SQL),
/// resuelve las claves foráneas numéricas (<c>clientes</c>, <c>transportes</c>, <c>ata</c>, <c>productos</c>,
/// <c>importadores_exportadores</c>, <c>procedencias</c>) y es idempotente si se corre más de una vez.
/// </summary>
public partial class Importador1x
{
    public const string OperadorImportado = "importado 1.x";

    private static readonly string[] FormatosFecha =
        ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];

    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    private readonly BfsDbContext _db;
    private readonly PasswordHasher<Usuario> _hasher;
    private readonly TimeZoneInfo _zona;
    private readonly string _carpetaBranding;
    private readonly Dictionary<string, ConteoImportacion> _conteos = new();
    private readonly List<string> _avisos = [];

    public Importador1x(BfsDbContext db, PasswordHasher<Usuario> hasher, string zonaHoraria, string carpetaBranding)
    {
        _db = db;
        _hasher = hasher;
        _carpetaBranding = carpetaBranding;
        try { _zona = TimeZoneInfo.FindSystemTimeZoneById(zonaHoraria); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { _zona = TimeZoneInfo.Local; }
    }

    /// <summary>True si el archivo parece un dump SQL de MySQL (y no una base SQLite de la 3.x).</summary>
    public static bool EsBase1x(string archivo)
    {
        if (!File.Exists(archivo)) return false;
        if (archivo.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) return true;

        Span<byte> cabecera = stackalloc byte[4096];
        using var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var leidos = fs.Read(cabecera);
        if (leidos >= SqliteMagic.Length && cabecera[..SqliteMagic.Length].SequenceEqual(SqliteMagic)) return false;

        var texto = Encoding.UTF8.GetString(cabecera[..leidos]);
        return texto.Contains("MySQL dump", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("CREATE TABLE `", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("INSERT INTO `taras`", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resumen rápido de registros en el dump antes de importar.</summary>
    public static Dictionary<string, long> Contar(string archivoSql)
    {
        var dump = DumpMysql.Leer(archivoSql, soloConteo: true);
        if (!dump.Conteos.ContainsKey("taras"))
            throw new InvalidOperationException("El archivo SQL no es una base de la 1.x (no tiene la tabla taras)");

        var nombres = new (string Tabla, string Etiqueta)[]
        {
            ("taras", "pesadas"),
            ("clientes", "clientes"),
            ("transportes", "transportes"),
            ("ata", "ata"),
            ("productos", "productos"),
            ("importadores_exportadores", "exportadores"),
            ("usuarios", "usuarios"),
            ("indicadores", "balanzas")
        };
        var res = new Dictionary<string, long>();
        foreach (var (tabla, etiqueta) in nombres)
        {
            if (dump.Conteos.TryGetValue(tabla, out var n)) res[etiqueta] = n;
        }
        return res;
    }

    public async Task<ResultadoImportacion> ImportarAsync(string archivoSql, Guid empresaId)
    {
        var dump = DumpMysql.Leer(archivoSql, soloConteo: false);
        if (!dump.TablasConocidas.Contains("taras"))
            throw new InvalidOperationException("El archivo SQL no es una base de la 1.x (no tiene la tabla taras)");

        var empresa = await _db.Empresas.FirstAsync(e => e.Id == empresaId);
        await using var tx = await _db.Database.BeginTransactionAsync();

        var hayAduanaEnPesadas = dump.Filas("taras")
            .Any(f => string.Equals(f.Str("modalidad"), "ADUANA", StringComparison.OrdinalIgnoreCase));
        var (transaccionActual, lotPlanta) = await ParametrosGlobales(dump, empresa, hayAduanaEnPesadas);

        var clientes = await Clientes(dump);
        var transportes = await TransportesYAtas(dump);
        var productos = await Productos(dump);
        var exportadores = await Exportadores(dump);
        var procedencias = Procedencias(dump);
        await TarasConocidas(dump);
        await Balanzas(dump);
        await Usuarios(dump);

        await PesadasYVehiculos(dump, empresaId, clientes, transportes.Transportes, transportes.Atas,
            productos, exportadores, procedencias, transaccionActual, lotPlanta);

        await tx.CommitAsync();
        return new ResultadoImportacion(_conteos, _avisos);
    }

    private async Task<(int TransaccionActual, string? Lot)> ParametrosGlobales(DumpMysql dump, Empresa empresa, bool hayAduanaEnPesadas)
    {
        var filas = dump.Filas("parametros_globales");
        if (filas.Count == 0) return (0, null);

        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var blobs = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            var id = f.Str("id");
            if (id == null) continue;
            if (f.Str("value") is { } v) valores[id] = v;
            if (f.Bytes("valueByte") is { Length: > 0 } b) blobs[id] = b;
        }

        // En la 1.x, EMPRESA_* sin _BAL trae los datos de Balanzas Full Service; los de la planta están en *_BAL.
        var nombreBal = ValorParam(valores, "EMPRESA_NOMBRE_BAL") ?? ValorClienteNoBfs(valores, "EMPRESA_NOMBRE");
        if (nombreBal != null)
        {
            var (razonLimpia, cuitEmbebido) = ExtraerCuitDeNombre(nombreBal);
            if (razonLimpia.Length > 0 && !await _db.Empresas.AnyAsync(e => e.Id != empresa.Id && e.Nombre.ToLower() == razonLimpia.ToLower()))
            {
                empresa.Nombre = razonLimpia;
            }
            if (cuitEmbebido != null)
            {
                empresa.Cuit = CuitTolerante(cuitEmbebido, "La empresa") ?? empresa.Cuit;
            }
        }

        empresa.Direccion = ValorParam(valores, "EMPRESA_DIR_BAL") ?? empresa.Direccion;
        empresa.Localidad = ValorParam(valores, "EMPRESA_LOC_BAL") ?? empresa.Localidad;
        empresa.Provincia = ValorParam(valores, "EMPRESA_PROV_BAL") ?? empresa.Provincia;
        empresa.Telefono = ValorParam(valores, "EMPRESA_TEL_BAL") ?? empresa.Telefono;

        var codigoAduana = ValorParam(valores, "CODIGO_ADUANA");
        var lot = ValorParam(valores, "CODIGO_LOG") ?? ValorParam(valores, "CODIGO_LOT");
        var certificado = ValorParam(valores, "CERTIFICADO");
        var vencimiento = Fecha(ValorParam(valores, "VENCIMIENTO")) is { } vto
            ? TimeZoneInfo.ConvertTime(vto, _zona).ToString("yyyy-MM-dd")
            : "";

        var usaAduana = hayAduanaEnPesadas || !string.IsNullOrEmpty(codigoAduana) || !string.IsNullOrEmpty(lot);
        var config = new Dictionary<string, string>
        {
            [ConfigKeys.ClienteNombre] = empresa.Nombre,
            [ConfigKeys.IngresoManual] = "true",
            [ConfigKeys.Termica] = BoolTexto(ValorParam(valores, "TICKET_ETIQUETADORA")) ? "true" : "false"
        };
        if (usaAduana)
        {
            config[ConfigKeys.Aduana] = "true";
            config[ConfigKeys.Contenedores] = "true";
        }
        if (codigoAduana != null) config[ConfigKeys.AduanaCodigo] = codigoAduana;
        if (lot != null) config[ConfigKeys.AduanaLot] = lot;
        if (certificado != null) config[ConfigKeys.AduanaCertificado1] = certificado;
        if (vencimiento.Length > 0) config[ConfigKeys.AduanaCertificado1Vence] = vencimiento;

        if (ValorParam(valores, "EMPRESA_ING_MANUAL") is { } claveManual)
        {
            config[ConfigKeys.ClaveManualHash] = _hasher.HashPassword(new Usuario(), claveManual);
        }

        // En la 1.x varias filas de parametros_globales guardaban por defecto el PNG institucional de BFS.
        // Solo copiamos EMPRESA_IMG si es una imagen distinta de ese blob por defecto.
        if (blobs.TryGetValue("EMPRESA_IMG", out var img) && EsImagenPropiaDelCliente(img, blobs))
        {
            var ext = ExtensionImagen(img);
            if (ext != null)
            {
                Directory.CreateDirectory(_carpetaBranding);
                await File.WriteAllBytesAsync(Path.Combine(_carpetaBranding, "logo" + ext), img);
                config[ConfigKeys.LogoFile] = "logo" + ext;
            }
        }

        await GuardarConfig(config);
        await _db.SaveChangesAsync();
        Contar("Empresa", 1, 0);

        var transaccion = int.TryParse(ValorParam(valores, "EMPRESA_TRANSACCION"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var txNum)
            ? txNum
            : 0;
        return (transaccion, lot);
    }

    private async Task<Dictionary<int, (string Nombre, Guid Id)>> Clientes(DumpMysql dump)
    {
        var existentes = (await _db.Clientes.IgnoreQueryFilters().ToListAsync())
            .GroupBy(c => c.RazonSocial.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var porCodigo = new Dictionary<int, (string Nombre, Guid Id)>();
        var filas = dump.Filas("clientes");
        if (filas.Count == 0) return porCodigo;

        int importados = 0, omitidos = 0;
        foreach (var f in filas)
        {
            var codigo = f.Int("codigo");
            var nombre = f.Str("nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                omitidos++;
                continue;
            }
            var clave = nombre.Trim().ToLowerInvariant();
            var cuit = CuitTolerante(LimpiarCuit(f.Str("CUIT")), "Cliente " + nombre);
            if (existentes.TryGetValue(clave, out var actual))
            {
                if (actual.Cuit == null && cuit != null) actual.Cuit = cuit;
                if (codigo.HasValue) porCodigo[codigo.Value] = (actual.RazonSocial, actual.Id);
                omitidos++;
                continue;
            }

            var nuevo = new Cliente
            {
                Id = Guid.NewGuid(),
                RazonSocial = nombre.Trim(),
                Cuit = cuit
            };
            _db.Clientes.Add(nuevo);
            existentes[clave] = nuevo;
            if (codigo.HasValue) porCodigo[codigo.Value] = (nuevo.RazonSocial, nuevo.Id);
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar("Clientes", importados, omitidos);
        return porCodigo;
    }

    private async Task<(Dictionary<int, string> Transportes, Dictionary<int, (string Nombre, string? Cuit)> Atas)> TransportesYAtas(DumpMysql dump)
    {
        var existentes = (await _db.Transportes.IgnoreQueryFilters().ToListAsync())
            .GroupBy(t => t.RazonSocial.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var transportesPorCodigo = new Dictionary<int, string>();
        var atasPorCodigo = new Dictionary<int, (string Nombre, string? Cuit)>();

        int impTr = 0, omiTr = 0;
        foreach (var f in dump.Filas("transportes"))
        {
            var codigo = f.Int("codigo");
            var nombre = f.Str("nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                omiTr++;
                continue;
            }
            var clave = nombre.Trim().ToLowerInvariant();
            var cuit = CuitTolerante(LimpiarCuit(f.Str("CUIT")), "Transporte " + nombre);
            if (existentes.TryGetValue(clave, out var actual))
            {
                if (actual.Cuit == null && cuit != null) actual.Cuit = cuit;
                if (codigo.HasValue) transportesPorCodigo[codigo.Value] = actual.RazonSocial;
                omiTr++;
                continue;
            }
            var nuevo = new Transporte
            {
                Id = Guid.NewGuid(),
                RazonSocial = nombre.Trim(),
                Cuit = cuit
            };
            _db.Transportes.Add(nuevo);
            existentes[clave] = nuevo;
            if (codigo.HasValue) transportesPorCodigo[codigo.Value] = nuevo.RazonSocial;
            impTr++;
        }
        if (dump.TablasConocidas.Contains("transportes")) Contar("Transportes", impTr, omiTr);

        int impAta = 0, omiAta = 0;
        foreach (var f in dump.Filas("ata"))
        {
            var codigo = f.Int("codigo");
            var nombre = f.Str("nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                omiAta++;
                continue;
            }
            var clave = nombre.Trim().ToLowerInvariant();
            var cuit = CuitTolerante(LimpiarCuit(f.Str("CUIT")), "ATA " + nombre);
            if (existentes.TryGetValue(clave, out var actual))
            {
                var promovido = !actual.EsAta;
                actual.EsAta = true;
                if (actual.Cuit == null && cuit != null) actual.Cuit = cuit;
                if (codigo.HasValue) atasPorCodigo[codigo.Value] = (actual.RazonSocial, actual.Cuit);
                if (promovido) impAta++; else omiAta++;
                continue;
            }
            var nuevo = new Transporte
            {
                Id = Guid.NewGuid(),
                RazonSocial = nombre.Trim(),
                Cuit = cuit,
                EsAta = true
            };
            _db.Transportes.Add(nuevo);
            existentes[clave] = nuevo;
            if (codigo.HasValue) atasPorCodigo[codigo.Value] = (nuevo.RazonSocial, nuevo.Cuit);
            impAta++;
        }
        if (dump.TablasConocidas.Contains("ata")) Contar("Agentes ATA", impAta, omiAta);

        await _db.SaveChangesAsync();
        return (transportesPorCodigo, atasPorCodigo);
    }

    private async Task<Dictionary<int, string>> Productos(DumpMysql dump)
    {
        var existentes = (await _db.Productos.IgnoreQueryFilters().ToListAsync())
            .GroupBy(p => p.Nombre.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var porCodigo = new Dictionary<int, string>();
        var filas = dump.Filas("productos");
        if (filas.Count == 0) return porCodigo;

        int importados = 0, omitidos = 0;
        foreach (var f in filas)
        {
            var codigo = f.Int("codigo");
            var nombre = f.Str("nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                omitidos++;
                continue;
            }
            var clave = nombre.Trim().ToLowerInvariant();
            var alias = LimpiarPlaceholder(f.Str("alias"));
            if (existentes.TryGetValue(clave, out var actual))
            {
                if (codigo.HasValue) porCodigo[codigo.Value] = actual.Nombre;
                omitidos++;
                continue;
            }
            var nuevo = new Producto
            {
                Id = Guid.NewGuid(),
                Nombre = nombre.Trim(),
                Codigo = alias != null && !string.Equals(alias, nombre.Trim(), StringComparison.OrdinalIgnoreCase) ? alias : null
            };
            _db.Productos.Add(nuevo);
            existentes[clave] = nuevo;
            if (codigo.HasValue) porCodigo[codigo.Value] = nuevo.Nombre;
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar("Productos", importados, omitidos);
        return porCodigo;
    }

    private async Task<Dictionary<int, (string Nombre, string? Cuit)>> Exportadores(DumpMysql dump)
    {
        var existentes = (await _db.Exportadores.IgnoreQueryFilters().ToListAsync())
            .GroupBy(e => e.RazonSocial.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var porCodigo = new Dictionary<int, (string Nombre, string? Cuit)>();
        var filas = dump.Filas("importadores_exportadores");
        if (filas.Count == 0) return porCodigo;

        int importados = 0, omitidos = 0;
        foreach (var f in filas)
        {
            var codigo = f.Int("codigo");
            var nombre = f.Str("nombre");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                omitidos++;
                continue;
            }
            var clave = nombre.Trim().ToLowerInvariant();
            var cuit = CuitTolerante(LimpiarCuit(f.Str("CUIT")), "Exportador " + nombre);
            if (existentes.TryGetValue(clave, out var actual))
            {
                if (actual.Cuit == null && cuit != null) actual.Cuit = cuit;
                if (codigo.HasValue) porCodigo[codigo.Value] = (actual.RazonSocial, actual.Cuit);
                omitidos++;
                continue;
            }
            var nuevo = new Exportador
            {
                Id = Guid.NewGuid(),
                RazonSocial = nombre.Trim(),
                Cuit = cuit
            };
            _db.Exportadores.Add(nuevo);
            existentes[clave] = nuevo;
            if (codigo.HasValue) porCodigo[codigo.Value] = (nuevo.RazonSocial, nuevo.Cuit);
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar("Exportadores", importados, omitidos);
        return porCodigo;
    }

    private static Dictionary<int, string> Procedencias(DumpMysql dump)
    {
        var res = new Dictionary<int, string>();
        foreach (var f in dump.Filas("procedencias"))
        {
            if (f.Int("codigo") is { } id && LimpiarPlaceholder(f.Str("nombre")) is { } nombre)
            {
                res[id] = nombre;
            }
        }
        return res;
    }

    private async Task TarasConocidas(DumpMysql dump)
    {
        var filas = dump.Filas("patentes");
        if (filas.Count == 0) return;

        var existentes = (await _db.Taras.IgnoreQueryFilters().Select(t => t.Identificador).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int importadas = 0, omitidas = 0;
        foreach (var f in filas)
        {
            var kg = f.PesoKg("tara") ?? 0;
            var patente = Patente.Normalizar(f.Str("codigo"));
            if (kg <= 0 || patente.Length == 0 || !existentes.Add(patente))
            {
                omitidas++;
                continue;
            }
            _db.Taras.Add(new TaraConocida
            {
                Id = Guid.NewGuid(),
                Identificador = patente,
                Kilogramos = kg,
                TomadaEn = Fecha(f.Str("date_update")) ?? DateTimeOffset.UtcNow
            });
            importadas++;
        }
        if (importadas > 0)
        {
            await _db.SaveChangesAsync();
            Contar("Taras", importadas, omitidas);
        }
    }

    private async Task Balanzas(DumpMysql dump)
    {
        var indicadores = dump.Filas("indicadores");
        if (indicadores.Count == 0) return;

        // Si existe la tabla comunicaciones, solo tomamos los indicadores vinculados (o todos si está vacía).
        var activos = dump.Filas("comunicaciones")
            .Select(c => c.Int("idindicadores"))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        int importadas = 0, omitidas = 0;
        var orden = await _db.Balanzas.MaxAsync(b => (int?)b.Orden) ?? 0;
        var simuladaInicial = await _db.Balanzas.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Modo == ModosBalanza.Simulada && string.IsNullOrEmpty(x.Puerto));

        foreach (var f in indicadores)
        {
            if (activos.Count > 0 && f.Int("idindicadores") is { } idInd && !activos.Contains(idInd)) continue;

            var nombre = f.Str("nombre") ?? "Balanza 1";
            var existente = await _db.Balanzas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Nombre.ToLower() == nombre.ToLower());
            if (existente != null && !(existente.Modo == ModosBalanza.Simulada && string.IsNullOrEmpty(existente.Puerto)))
            {
                omitidas++;
                continue;
            }

            var b = existente ?? simuladaInicial;
            if (b == simuladaInicial) simuladaInicial = null;
            if (b == null)
            {
                b = new Balanza { Id = Guid.NewGuid(), Orden = ++orden };
                _db.Balanzas.Add(b);
            }

            var numPuerto = f.Int("puerto");
            var puerto = numPuerto is > 0 ? $"COM{numPuerto}" : (f.Str("puerto")?.ToUpperInvariant() ?? "");
            b.Nombre = nombre;
            b.Eliminado = false;
            b.Activa = true;
            b.Modo = puerto.Length > 0 ? ModosBalanza.Serie : ModosBalanza.Simulada;
            b.Puerto = puerto;
            b.Baudios = f.Int("velocidad") ?? 9600;
            b.DataBits = f.Int("bits_de_datos") ?? 8;
            b.Paridad = Paridad1x(f.Str("paridad"));
            b.StopBits = f.Int("bits_parada") is 2 ? 2 : 1;
            b.Protocolo = ProtocolosBalanza.Line;
            importadas++;
        }

        await _db.SaveChangesAsync();
        Contar("Balanzas", importadas, omitidas);
        if (importadas > 0)
        {
            _avisos.Add("Revisá el puerto COM y el protocolo de lectura del indicador en Configuración → Balanzas.");
        }
    }

    private async Task Usuarios(DumpMysql dump)
    {
        var filas = dump.Filas("usuarios");
        if (filas.Count == 0) return;

        int importados = 0, omitidos = 0;
        foreach (var f in filas)
        {
            var nombreOriginal = f.Str("nombre")?.Trim();
            var login = Usuario.NormalizarEmail(nombreOriginal);
            if (login.Length == 0)
            {
                omitidos++;
                continue;
            }

            if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == login && u.Rol == Roles.Vendor))
            {
                var renombrado = login + "-1x";
                _avisos.Add($"El usuario {login} de la 1.x se importó como {renombrado} ({login} es el usuario de BFS en la 4.0)");
                login = renombrado;
            }
            if (await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == login))
            {
                omitidos++;
                continue;
            }

            var rol = (f.Int("id_perfil") ?? 3) switch
            {
                1 => Roles.Admin,
                2 => Roles.Supervisor,
                _ => Roles.Operador
            };

            var u = new Usuario
            {
                Id = Guid.NewGuid(),
                Email = login,
                Nombre = nombreOriginal!,
                Activo = true,
                Rol = rol
            };

            var clave = f.Str("clave");
            if (string.IsNullOrEmpty(clave))
            {
                clave = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
                _avisos.Add($"El usuario {login} no tenía contraseña: asignale una desde Usuarios");
            }
            u.PasswordHash = _hasher.HashPassword(u, clave);
            _db.Usuarios.Add(u);
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar("Usuarios", importados, omitidos);
        if (importados > 0)
        {
            _avisos.Add("Los usuarios de la 1.x entran con su mismo nombre y clave.");
        }
    }

    private async Task PesadasYVehiculos(
        DumpMysql dump,
        Guid empresaId,
        Dictionary<int, (string Nombre, Guid Id)> clientes,
        Dictionary<int, string> transportes,
        Dictionary<int, (string Nombre, string? Cuit)> atas,
        Dictionary<int, string> productos,
        Dictionary<int, (string Nombre, string? Cuit)> exportadores,
        Dictionary<int, string> procedencias,
        int transaccionActual,
        string? lotPlanta)
    {
        // (EmpresaId, TicketNumero) es único en la 4.0. En la 1.x a veces dos camiones distintos salieron con el mismo
        // número de transacción; guardamos TicketNumero -> EntradaEn.UtcTicks para que:
        // - si coincide la fecha de entrada, ya estaba importada (idempotente al correr dos veces);
        // - si es otra fecha, se guarda como "{ticket}-2" sin perder la pesada.
        var ticketsUsados = (await _db.Pesadas.IgnoreQueryFilters()
                .Where(p => p.EmpresaId == empresaId)
                .Select(p => new { p.TicketNumero, p.EntradaEn })
                .ToListAsync())
            .ToDictionary(x => x.TicketNumero, x => x.EntradaEn.UtcTicks, StringComparer.OrdinalIgnoreCase);

        var vehiculosExistentes = (await _db.DatosVehiculos.IgnoreQueryFilters()
                .Select(d => d.PatenteChasis)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ultimosVehiculos = new Dictionary<string, (DateTimeOffset Fecha, DatosVehiculo Datos)>(StringComparer.OrdinalIgnoreCase);

        int importadas = 0, omitidas = 0, sinFecha = 0, renumeradas = 0;
        var maximoTicket = transaccionActual;

        var autoDetectPrevio = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var f in dump.Filas("taras"))
            {
                var ticketBase = LimpiarPlaceholder(f.Str("transaccion")) ?? f.Str("idtaras") ?? "0";
                if (int.TryParse(ticketBase, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numTicket))
                {
                    maximoTicket = Math.Max(maximoTicket, numTicket);
                }

                var entrada = Fecha(f.Str("fecha_entrada"));
                if (entrada == null)
                {
                    sinFecha++;
                    entrada = DateTimeOffset.UnixEpoch;
                }

                var ticketFinal = ResolverTicket(ticketsUsados, ticketBase, entrada.Value.UtcTicks, out var yaImportada, out var fueRenumerada);
                if (yaImportada)
                {
                    omitidas++;
                    continue;
                }
                if (fueRenumerada) renumeradas++;

                var textoSalida = f.Str("fecha_salida");
                var pendiente = string.IsNullOrWhiteSpace(textoSalida) || textoSalida.StartsWith("0000", StringComparison.Ordinal);
                var pesoEntrada = f.PesoKg("peso_entrada") ?? 0;
                var pesoSalida = pendiente ? null : f.PesoKg("peso_salida");

                var esAduana = string.Equals(f.Str("modalidad"), "ADUANA", StringComparison.OrdinalIgnoreCase);
                var modoTara = f.Str("modoTara")?.ToUpperInvariant() is "TARA" or "TARA CONOCIDA"
                    ? ModosPesaje.TaraConocida
                    : ModosPesaje.Normal;

                string? clienteNombre = null;
                Guid? clienteId = null;
                if (f.Int("id_cliente") is { } idCli && clientes.TryGetValue(idCli, out var cli))
                {
                    clienteNombre = cli.Nombre;
                    clienteId = cli.Id;
                }

                var transporteNombre = f.Int("id_transporte") is { } idTr && transportes.TryGetValue(idTr, out var tr) ? tr : null;
                var productoNombre = (f.Int("id_producto") is { } idPr && productos.TryGetValue(idPr, out var pr) ? pr : null)
                    ?? LimpiarPlaceholder(f.Str("mercaderia"));
                var procedenciaNombre = f.Int("id_procedencia") is { } idProc && procedencias.TryGetValue(idProc, out var proc) ? proc : null;

                var chasis = Patente.Normalizar(LimpiarPatente(f.Str("patente")));
                var acopladoRaw = LimpiarPatente(f.Str("patente_aceptado"));
                var acoplado = acopladoRaw != null && Patente.Normalizar(acopladoRaw) is { Length: > 0 } ac ? ac : null;
                var conductor = LimpiarPlaceholder(f.Str("conductor"));
                var tipoDoc = LimpiarPlaceholder(f.Str("tipo_doc"));
                var numDoc = LimpiarPlaceholder(f.Str("num_doc"));
                var nacionalidad = LimpiarPlaceholder(f.Str("nacionalidad"));
                var destino = LimpiarPlaceholder(f.Str("destino"));

                var tipoMovimiento = !pendiente && pesoSalida.HasValue && pesoSalida.Value > pesoEntrada ? "EGRESO" : "INGRESO";

                var p = new Pesada
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    TicketNumero = ticketFinal,
                    Tipo = tipoMovimiento,
                    Estado = pendiente ? EstadosPesada.Entrada : EstadosPesada.Salida,
                    Modo = modoTara,
                    TipoOperacion = esAduana ? TiposOperacion.Aduana : TiposOperacion.Normal,
                    PatenteChasis = chasis,
                    PatenteAcoplado = acoplado,
                    Conductor = conductor,
                    TipoDocumento = tipoDoc,
                    Documento = numDoc,
                    Nacionalidad = nacionalidad,
                    Cliente = clienteNombre,
                    ClienteId = clienteId,
                    Transporte = transporteNombre,
                    Producto = productoNombre,
                    Procedencia = procedenciaNombre,
                    Destino = destino,
                    Comprobante = LimpiarPlaceholder(f.Str("comprobante_nun1")),
                    Observaciones = LimpiarPlaceholder(f.Str("observacion")),
                    Balanza = LimpiarPlaceholder(f.Str("balanza")),
                    BalanzaSalida = pendiente ? null : LimpiarPlaceholder(f.Str("balanza")),
                    Operador = OperadorImportado,
                    OperadorSalida = pendiente ? null : OperadorImportado,
                    PesoEntrada = pesoEntrada,
                    EntradaEn = entrada.Value,
                    CreadoEn = entrada.Value
                };

                if (!pendiente)
                {
                    p.PesoSalida = pesoSalida;
                    p.SalidaEn = Fecha(textoSalida) ?? entrada.Value;
                    if (pesoSalida.HasValue)
                    {
                        (p.Bruto, p.Tara, p.Neto) = NetoCalculator.Calcular(p.PesoEntrada, pesoSalida.Value, 0);
                    }
                }

                if (esAduana)
                {
                    var exp = f.Int("id_imp_exp") is { } idExp && exportadores.TryGetValue(idExp, out var ex) ? ex : (null, null);
                    var ata = f.Int("id_ata") is { } idAta && atas.TryGetValue(idAta, out var at) ? at : (null, null);

                    // En la 1.x contenedor_num se usaba para la tara del contenedor en kg (ej. "3700") y contenedor para el código ISO.
                    var contNum = LimpiarPlaceholder(f.Str("contenedor_num"));
                    var contenedorTara = 0;
                    string? contenedorFallback = null;
                    if (contNum != null)
                    {
                        if (double.TryParse(contNum.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var taraCont) && taraCont > 0)
                            contenedorTara = (int)Math.Round(taraCont, MidpointRounding.AwayFromZero);
                        else
                            contenedorFallback = contNum;
                    }

                    var contenedorId = LimpiarPlaceholder(f.Str("contenedor")) ?? contenedorFallback;
                    var tieneContenedor = contenedorTara > 0 || contenedorId != null;
                    var escenario = tieneContenedor
                        ? (tipoMovimiento == "EGRESO" ? EscenariosAduana.CargaContenedorLleno : EscenariosAduana.IngresaConContenedor)
                        : "";

                    int? contBruto = null, contNeto = null;
                    if (!pendiente && p.Neto is { } netoPesada && escenario.Length > 0
                        && EscenariosAduana.Contenedor(escenario, netoPesada, contenedorTara) is var (cb, cn))
                    {
                        contBruto = cb;
                        contNeto = cn;
                    }

                    p.Aduana = new DatosAduana
                    {
                        Escenario = escenario,
                        Exportador = exp.Item1,
                        ExportadorCuit = exp.Item2,
                        Ata = ata.Item1,
                        AtaCuit = ata.Item2,
                        OperacionId = LimpiarPlaceholder(f.Str("manifiesto")),
                        DestinacionAduanera = LimpiarPlaceholder(f.Str("observacion_aduana")),
                        ProcedenciaDestino = destino ?? procedenciaNombre,
                        Lot = lotPlanta,
                        ChoferTipoDocumento = tipoDoc,
                        ChoferNacionalidad = nacionalidad,
                        ContenedorId = contenedorId,
                        ContenedorTara = contenedorTara,
                        ContenedorBruto = contBruto,
                        ContenedorNeto = contNeto
                    };
                }

                if (chasis.Length >= 5 && !vehiculosExistentes.Contains(chasis)
                    && (conductor != null || acoplado != null || transporteNombre != null))
                {
                    if (!ultimosVehiculos.TryGetValue(chasis, out var previo) || entrada.Value >= previo.Fecha)
                    {
                        ultimosVehiculos[chasis] = (entrada.Value, new DatosVehiculo
                        {
                            Id = Guid.NewGuid(),
                            PatenteChasis = chasis,
                            PatenteAcoplado = acoplado,
                            Conductor = conductor,
                            Documento = numDoc,
                            TipoDocumento = tipoDoc,
                            Nacionalidad = nacionalidad,
                            Transporte = transporteNombre
                        });
                    }
                }

                _db.Pesadas.Add(p);
                importadas++;

                if (importadas % 2500 == 0)
                {
                    await _db.SaveChangesAsync();
                    _db.ChangeTracker.Clear();
                }
            }

            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();

            var vehiculosImportados = 0;
            foreach (var (_, item) in ultimosVehiculos)
            {
                _db.DatosVehiculos.Add(item.Datos);
                vehiculosImportados++;
                if (vehiculosImportados % 2500 == 0)
                {
                    await _db.SaveChangesAsync();
                    _db.ChangeTracker.Clear();
                }
            }
            if (vehiculosImportados > 0)
            {
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
                Contar("Datos de vehículos", vehiculosImportados, 0);
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = autoDetectPrevio;
        }

        Contar("Pesadas", importadas, omitidas);

        if (maximoTicket > 0)
        {
            var proximo = maximoTicket + 1;
            var contador = await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Puesto == 1);
            if (contador == null)
            {
                contador = new ContadorTicket { Id = Guid.NewGuid(), EmpresaId = empresaId, Puesto = 1, Proximo = proximo };
                _db.ContadoresTicket.Add(contador);
            }
            else
            {
                contador.Proximo = Math.Max(contador.Proximo, proximo);
            }

            var emp = await _db.Empresas.FirstAsync(e => e.Id == empresaId);
            emp.ProximoTicket = Math.Max(emp.ProximoTicket, proximo);
            await _db.SaveChangesAsync();
        }

        if (renumeradas > 0)
            _avisos.Add($"{renumeradas} pesadas tenían número de transacción repetido en la 1.x y se guardaron con sufijo (-2) para no perder ninguna.");
        if (sinFecha > 0)
            _avisos.Add($"{sinFecha} pesadas no tenían una fecha legible y quedaron con fecha 01/01/1970.");
    }

    private static string ResolverTicket(Dictionary<string, long> usados, string baseTicket, long entradaTicks, out bool yaImportada, out bool fueRenumerada)
    {
        var candidato = baseTicket;
        var sufijo = 1;
        while (usados.TryGetValue(candidato, out var existenteTicks))
        {
            if (existenteTicks == entradaTicks)
            {
                yaImportada = true;
                fueRenumerada = false;
                return candidato;
            }
            sufijo++;
            candidato = $"{baseTicket}-{sufijo}";
        }
        usados[candidato] = entradaTicks;
        yaImportada = false;
        fueRenumerada = sufijo > 1;
        return candidato;
    }

    private async Task GuardarConfig(Dictionary<string, string> valores)
    {
        foreach (var (clave, valor) in valores)
        {
            var row = await _db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == clave && c.EmpresaId == null);
            if (row == null) _db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = clave, Valor = valor });
            else row.Valor = valor;
        }
    }

    private string? CuitTolerante(string? cuit, string de)
    {
        if (cuit == null) return null;
        var (valor, error) = Cuit.Normalizar(cuit);
        if (error == null) return valor;
        _avisos.Add($"{de}: CUIT \"{cuit}\" no es válido, se importó tal cual");
        return cuit.Trim();
    }

    private void Contar(string que, int importados, int omitidos) => _conteos[que] = new ConteoImportacion(importados, omitidos);

    private DateTimeOffset? Fecha(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.StartsWith("0000", StringComparison.Ordinal)) return null;
        if (!DateTime.TryParseExact(texto.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        return new DateTimeOffset(local, _zona.GetUtcOffset(local)).ToUniversalTime();
    }

    private static string Paridad1x(string? p) => p?.Trim().ToLowerInvariant() switch
    {
        "e" or "even" => "Even",
        "o" or "odd" => "Odd",
        "m" or "mark" => "Mark",
        "s" or "space" => "Space",
        _ => "None"
    };

    private static string? ValorParam(Dictionary<string, string> d, string clave) =>
        d.TryGetValue(clave, out var v) && v.Trim() is { Length: > 0 } s ? s : null;

    private static string? ValorClienteNoBfs(Dictionary<string, string> d, string clave) =>
        ValorParam(d, clave) is { } s && !s.Contains("BALANZAS FULL SERVICE", StringComparison.OrdinalIgnoreCase) ? s : null;

    private static (string RazonSocial, string? Cuit) ExtraerCuitDeNombre(string texto)
    {
        var m = RegexCuitEnTexto().Match(texto);
        if (!m.Success) return (texto.Trim(), null);
        var cuit = m.Groups[1].Value.Trim();
        var limpio = (texto[..m.Index] + texto[(m.Index + m.Length)..]).Trim(' ', '-', '–', '/', '|', ',');
        return (limpio.Length > 0 ? limpio : texto.Trim(), cuit);
    }

    [GeneratedRegex(@"[-–/|,]?\s*CUIT\s*:?\s*([0-9]{2}-?[0-9]{7,8}-?[0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex RegexCuitEnTexto();

    private static string? LimpiarCuit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        // En la 1.x solían repetir el apodo en el campo CUIT ("USA", "TTE") o cargar "30-00000000-0" cuando no lo tenían.
        var digitos = s.Count(char.IsDigit);
        if (digitos < 8 || s.Replace("-", "").Contains("0000000", StringComparison.Ordinal)) return null;
        return s;
    }

    private static string? LimpiarPlaceholder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        return s is "0" or "XX" or "S/D" ? null : s;
    }

    private static string? LimpiarPatente(string? raw)
    {
        var s = LimpiarPlaceholder(raw);
        return s is null or "S/P" or "SIN" or "S/N" ? null : s;
    }

    private static bool BoolTexto(string? s) =>
        s?.Trim() is "1" or "true" or "True" or "TRUE" or "S" or "Si" or "Sí";

    private static bool EsImagenPropiaDelCliente(byte[] img, Dictionary<string, byte[]> blobs)
    {
        if (ExtensionImagen(img) == null) return false;
        foreach (var otraClave in new[] { "ACTIVAR_DEBUG", "EMPRESA_ING_MANUAL", "TICKET_ETIQUETADORA" })
        {
            if (blobs.TryGetValue(otraClave, out var defecto) && defecto.AsSpan().SequenceEqual(img))
                return false;
        }
        return true;
    }

    private static string? ExtensionImagen(byte[] bytes)
    {
        if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        return null;
    }

    /// <summary>Fila de un <c>INSERT INTO</c> asociada al orden de columnas leído en <c>CREATE TABLE</c>.</summary>
    private readonly struct FilaSql(Dictionary<string, int> columnas, byte[]?[] celdas)
    {
        public byte[]? Bytes(string col) =>
            columnas.TryGetValue(col, out var idx) && idx < celdas.Length ? celdas[idx] : null;

        public string? Str(string col)
        {
            var b = Bytes(col);
            if (b == null || b.Length == 0) return null;
            var s = Encoding.UTF8.GetString(b).Trim();
            return s.Length > 0 ? s : null;
        }

        public int? Int(string col) => Str(col) is { } s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

        public int? PesoKg(string col) =>
            Str(col) is { } s && double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? (int)Math.Round(d, MidpointRounding.AwayFromZero)
                : null;
    }

    /// <summary>Lector binario liviano de dumps <c>mysqldump</c> sin requerir servidor MySQL.</summary>
    private sealed class DumpMysql
    {
        private static readonly Dictionary<string, string[]> EsquemaPorDefecto = new(StringComparer.OrdinalIgnoreCase)
        {
            ["parametros_globales"] = ["id", "value", "valueByte"],
            ["clientes"] = ["codigo", "nombre", "CUIT"],
            ["transportes"] = ["codigo", "nombre", "CUIT", "ultimo_movimiento", "acumulado"],
            ["ata"] = ["codigo", "nombre", "CUIT", "ultimo_movimiento", "acumulado", "nacionalidad"],
            ["productos"] = ["codigo", "nombre", "alias", "ultimo_movimiento", "acumulado"],
            ["importadores_exportadores"] = ["codigo", "nombre", "CUIT", "ultimo_movimiento", "acumulado"],
            ["procedencias"] = ["codigo", "nombre"],
            ["patentes"] = ["codigo", "tara", "date_update", "dias"],
            ["usuarios"] = ["id", "nombre", "clave", "id_perfil"],
            ["comunicaciones"] = ["idcomunicaciones", "nombre", "idindicadores"],
            ["indicadores"] =
            [
                "idindicadores", "nombre", "posicion_caracter_control", "longitud_caracter_control",
                "caracter_control", "posicion_inicio_dato", "longitud_dato", "puerto", "velocidad",
                "bits_de_datos", "paridad", "control_de_flujo", "bits_parada"
            ],
            ["taras"] =
            [
                "idtaras", "transaccion", "fecha_entrada", "fecha_salida", "balanza",
                "id_producto", "id_cliente", "id_transporte", "id_procedencia", "id_imp_exp",
                "modalidad", "comprobante_nun1", "modoTara", "destino", "conductor",
                "tipo_doc", "num_doc", "patente", "patente_aceptado", "observacion",
                "observacion_aduana", "contenedor_num", "peso_entrada", "peso_salida", "update_date",
                "modoChasis", "id_ata", "contenedor", "manifiesto", "mercaderia", "nacionalidad"
            ]
        };

        private readonly Dictionary<string, Dictionary<string, int>> _columnas = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<byte[]?[]>> _filas = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, long> Conteos { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> TablasConocidas { get; } = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<FilaSql> Filas(string tabla)
        {
            if (!_filas.TryGetValue(tabla, out var crudas) || crudas.Count == 0) return [];
            var mapa = MapaColumnas(tabla);
            var lista = new List<FilaSql>(crudas.Count);
            foreach (var c in crudas) lista.Add(new FilaSql(mapa, c));
            return lista;
        }

        private Dictionary<string, int> MapaColumnas(string tabla)
        {
            if (_columnas.TryGetValue(tabla, out var m)) return m;
            if (EsquemaPorDefecto.TryGetValue(tabla, out var def))
            {
                m = Indexar(def);
                _columnas[tabla] = m;
                return m;
            }
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, int> Indexar(IReadOnlyList<string> cols)
        {
            var d = new Dictionary<string, int>(cols.Count, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cols.Count; i++) d[cols[i]] = i;
            return d;
        }

        public static DumpMysql Leer(string archivo, bool soloConteo)
        {
            if (!File.Exists(archivo)) throw new FileNotFoundException("No se encontró el backup SQL de la 1.x", archivo);
            byte[] data;
            using (var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length >= SqliteMagic.Length)
                {
                    Span<byte> hdr = stackalloc byte[SqliteMagic.Length];
                    if (fs.Read(hdr) == SqliteMagic.Length && hdr.SequenceEqual(SqliteMagic))
                        throw new InvalidOperationException("El archivo es una base SQLite (3.x), no un dump SQL de la 1.x.");
                    fs.Position = 0;
                }
                data = new byte[fs.Length];
                fs.ReadExactly(data);
            }

            var dump = new DumpMysql();
            dump.Parsear(data, soloConteo);
            return dump;
        }

        private void Parsear(ReadOnlySpan<byte> s, bool soloConteo)
        {
            var i = 0;
            var n = s.Length;
            while (i < n)
            {
                SaltarBlancosYComentarios(s, ref i);
                if (i >= n) break;

                if (CoincidePalabra(s, i, "CREATE"u8))
                {
                    LeerCreateTable(s, ref i);
                }
                else if (CoincidePalabra(s, i, "INSERT"u8))
                {
                    LeerInsert(s, ref i, soloConteo);
                }
                else
                {
                    AvanzarHastaPuntoYComa(s, ref i);
                }
            }
        }

        private void LeerCreateTable(ReadOnlySpan<byte> s, ref int i)
        {
            var inicio = i;
            AvanzarHastaPuntoYComa(s, ref i);
            var bloque = Encoding.UTF8.GetString(s[inicio..Math.Min(i, s.Length)]);
            var idxTabla = bloque.IndexOf("TABLE", StringComparison.OrdinalIgnoreCase);
            if (idxTabla < 0) return;

            var pos = idxTabla + 5;
            if (bloque.AsSpan(pos).TrimStart().StartsWith("IF NOT EXISTS", StringComparison.OrdinalIgnoreCase))
            {
                pos = bloque.IndexOf("EXISTS", pos, StringComparison.OrdinalIgnoreCase) + 6;
            }
            var nombre = LeerIdentificadorTexto(bloque, ref pos);
            if (string.IsNullOrEmpty(nombre)) return;
            TablasConocidas.Add(nombre);

            var abre = bloque.IndexOf('(', pos);
            var cierra = bloque.LastIndexOf(')');
            if (abre < 0 || cierra <= abre) return;

            var cols = new List<string>();
            foreach (var linea in bloque[(abre + 1)..cierra].Split('\n'))
            {
                var t = linea.TrimStart();
                if (t.StartsWith('`'))
                {
                    var fin = t.IndexOf('`', 1);
                    if (fin > 1) cols.Add(t[1..fin]);
                }
            }
            if (cols.Count > 0) _columnas[nombre] = Indexar(cols);
        }

        private void LeerInsert(ReadOnlySpan<byte> s, ref int i, bool soloConteo)
        {
            i += 6; // INSERT
            SaltarBlancosYComentarios(s, ref i);
            if (CoincidePalabra(s, i, "IGNORE"u8)) { i += 6; SaltarBlancosYComentarios(s, ref i); }
            if (CoincidePalabra(s, i, "INTO"u8)) { i += 4; SaltarBlancosYComentarios(s, ref i); }

            var tabla = LeerIdentificadorBytes(s, ref i);
            if (string.IsNullOrEmpty(tabla))
            {
                AvanzarHastaPuntoYComa(s, ref i);
                return;
            }
            TablasConocidas.Add(tabla);
            SaltarBlancosYComentarios(s, ref i);

            // Columnas explícitas opcionales: INSERT INTO `tabla` (`c1`, `c2`) VALUES ...
            Dictionary<string, int>? mapaInsert = null;
            if (i < s.Length && s[i] == (byte)'(')
            {
                var cols = new List<string>();
                i++;
                while (i < s.Length && s[i] != (byte)')')
                {
                    SaltarBlancos(s, ref i);
                    var col = LeerIdentificadorBytes(s, ref i);
                    if (col.Length > 0) cols.Add(col);
                    SaltarBlancos(s, ref i);
                    if (i < s.Length && s[i] == (byte)',') i++;
                }
                if (i < s.Length && s[i] == (byte)')') i++;
                if (cols.Count > 0)
                {
                    mapaInsert = Indexar(cols);
                    if (!_columnas.ContainsKey(tabla)) _columnas[tabla] = mapaInsert;
                }
                SaltarBlancosYComentarios(s, ref i);
            }

            if (!CoincidePalabra(s, i, "VALUES"u8))
            {
                AvanzarHastaPuntoYComa(s, ref i);
                return;
            }
            i += 6;

            int[]? reorden = null;
            if (mapaInsert != null && _columnas.TryGetValue(tabla, out var mapaTabla) && !ReferenceEquals(mapaInsert, mapaTabla))
            {
                reorden = new int[mapaInsert.Count];
                foreach (var (nombreCol, posIns) in mapaInsert)
                    reorden[posIns] = mapaTabla.TryGetValue(nombreCol, out var posTab) ? posTab : -1;
            }
            var totalColsTabla = _columnas.TryGetValue(tabla, out var mt) ? mt.Count : 0;

            if (!soloConteo && !_filas.ContainsKey(tabla)) _filas[tabla] = [];
            Conteos.TryGetValue(tabla, out var cuenta);

            var n = s.Length;
            while (i < n)
            {
                SaltarBlancos(s, ref i);
                if (i >= n) break;
                if (s[i] == (byte)';') { i++; break; }
                if (s[i] == (byte)',') { i++; continue; }
                if (s[i] != (byte)'(') { AvanzarHastaPuntoYComa(s, ref i); break; }

                if (soloConteo)
                {
                    SaltarTupla(s, ref i);
                    cuenta++;
                }
                else
                {
                    var celdas = LeerTupla(s, ref i);
                    if (reorden != null && totalColsTabla > 0)
                    {
                        var alineadas = new byte[]?[totalColsTabla];
                        for (var c = 0; c < celdas.Length && c < reorden.Length; c++)
                        {
                            if (reorden[c] >= 0) alineadas[reorden[c]] = celdas[c];
                        }
                        celdas = alineadas;
                    }
                    _filas[tabla].Add(celdas);
                    cuenta++;
                }
            }
            Conteos[tabla] = cuenta;
        }

        private static void SaltarTupla(ReadOnlySpan<byte> s, ref int i)
        {
            i++; // '('
            var n = s.Length;
            var enComillas = false;
            while (i < n)
            {
                var b = s[i];
                if (enComillas)
                {
                    if (b == (byte)'\\') { i += 2; continue; }
                    if (b == (byte)'\'') enComillas = false;
                }
                else
                {
                    if (b == (byte)'\'') enComillas = true;
                    else if (b == (byte)')') { i++; return; }
                }
                i++;
            }
        }

        private static byte[]?[] LeerTupla(ReadOnlySpan<byte> s, ref int i)
        {
            i++; // '('
            var n = s.Length;
            var valores = new List<byte[]?>(32);
            while (i < n)
            {
                SaltarBlancos(s, ref i);
                if (i >= n) break;

                if (s[i] == (byte)'\'')
                {
                    valores.Add(LeerCadenaMysql(s, ref i));
                }
                else
                {
                    var inicio = i;
                    while (i < n && s[i] != (byte)',' && s[i] != (byte)')') i++;
                    var token = RecortarAscii(s[inicio..i]);
                    valores.Add(EsNull(token) ? null : token.ToArray());
                }

                SaltarBlancos(s, ref i);
                if (i < n && s[i] == (byte)',') i++;
                else if (i < n && s[i] == (byte)')') { i++; break; }
            }
            return valores.ToArray();
        }

        private static byte[] LeerCadenaMysql(ReadOnlySpan<byte> s, ref int i)
        {
            i++; // '\''
            var n = s.Length;
            var inicio = i;
            // Camino rápido sin escapes ni comillas dobles (la gran mayoría de las celdas).
            var p = i;
            var simple = true;
            while (p < n)
            {
                var b = s[p];
                if (b == (byte)'\\') { simple = false; break; }
                if (b == (byte)'\'')
                {
                    if (p + 1 < n && s[p + 1] == (byte)'\'') { simple = false; break; }
                    i = p + 1;
                    return s[inicio..p].ToArray();
                }
                p++;
            }

            if (simple)
            {
                i = n;
                return s[inicio..].ToArray();
            }

            using var ms = new MemoryStream(Math.Max(32, p - inicio));
            if (p > inicio) ms.Write(s[inicio..p]);
            i = p;
            while (i < n)
            {
                var b = s[i];
                if (b == (byte)'\\' && i + 1 < n)
                {
                    var esc = s[i + 1];
                    ms.WriteByte(esc switch
                    {
                        (byte)'0' => 0,
                        (byte)'b' => (byte)'\b',
                        (byte)'n' => (byte)'\n',
                        (byte)'r' => (byte)'\r',
                        (byte)'t' => (byte)'\t',
                        (byte)'Z' => 26,
                        _ => esc
                    });
                    i += 2;
                }
                else if (b == (byte)'\'')
                {
                    if (i + 1 < n && s[i + 1] == (byte)'\'')
                    {
                        ms.WriteByte((byte)'\'');
                        i += 2;
                    }
                    else
                    {
                        i++;
                        break;
                    }
                }
                else
                {
                    ms.WriteByte(b);
                    i++;
                }
            }
            return ms.ToArray();
        }

        private static void AvanzarHastaPuntoYComa(ReadOnlySpan<byte> s, ref int i)
        {
            var n = s.Length;
            byte comilla = 0;
            while (i < n)
            {
                var b = s[i];
                if (comilla != 0)
                {
                    if (b == (byte)'\\') { i += 2; continue; }
                    if (b == comilla) comilla = 0;
                }
                else
                {
                    if (b is (byte)'\'' or (byte)'"' or (byte)'`') comilla = b;
                    else if (b == (byte)';') { i++; return; }
                }
                i++;
            }
        }

        private static void SaltarBlancos(ReadOnlySpan<byte> s, ref int i)
        {
            while (i < s.Length && s[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') i++;
        }

        private static void SaltarBlancosYComentarios(ReadOnlySpan<byte> s, ref int i)
        {
            var n = s.Length;
            while (i < n)
            {
                SaltarBlancos(s, ref i);
                if (i >= n) return;

                // BOM UTF-8 al inicio del archivo
                if (i == 0 && i + 2 < n && s[0] == 0xEF && s[1] == 0xBB && s[2] == 0xBF)
                {
                    i += 3;
                    continue;
                }
                if (s[i] == (byte)'#')
                {
                    while (i < n && s[i] != (byte)'\n') i++;
                    continue;
                }
                if (i + 1 < n && s[i] == (byte)'-' && s[i + 1] == (byte)'-')
                {
                    while (i < n && s[i] != (byte)'\n') i++;
                    continue;
                }
                if (i + 1 < n && s[i] == (byte)'/' && s[i + 1] == (byte)'*')
                {
                    // Comentarios ejecutables /*!40101 SET ... */; los salteamos completos hasta el ';'.
                    var ejecutable = i + 2 < n && s[i + 2] == (byte)'!';
                    i += 2;
                    while (i + 1 < n && !(s[i] == (byte)'*' && s[i + 1] == (byte)'/')) i++;
                    if (i + 1 < n) i += 2;
                    if (ejecutable)
                    {
                        SaltarBlancos(s, ref i);
                        if (i < n && s[i] == (byte)';') i++;
                    }
                    continue;
                }
                return;
            }
        }

        private static bool CoincidePalabra(ReadOnlySpan<byte> s, int pos, ReadOnlySpan<byte> palabra)
        {
            if (pos + palabra.Length > s.Length) return false;
            for (var k = 0; k < palabra.Length; k++)
            {
                var a = s[pos + k];
                if (a is >= (byte)'a' and <= (byte)'z') a = (byte)(a - 32);
                if (a != palabra[k]) return false;
            }
            if (pos + palabra.Length == s.Length) return true;
            var sig = s[pos + palabra.Length];
            return sig is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'`' or (byte)'(';
        }

        private static string LeerIdentificadorBytes(ReadOnlySpan<byte> s, ref int i)
        {
            SaltarBlancos(s, ref i);
            if (i >= s.Length) return "";
            if (s[i] == (byte)'`')
            {
                i++;
                var inicio = i;
                while (i < s.Length && s[i] != (byte)'`') i++;
                var id = Encoding.UTF8.GetString(s[inicio..i]);
                if (i < s.Length) i++;
                return id;
            }
            var ini = i;
            while (i < s.Length && (char.IsLetterOrDigit((char)s[i]) || s[i] == (byte)'_')) i++;
            return Encoding.UTF8.GetString(s[ini..i]);
        }

        private static string LeerIdentificadorTexto(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) return "";
            if (s[i] == '`')
            {
                i++;
                var fin = s.IndexOf('`', i);
                if (fin < 0) return "";
                var id = s[i..fin];
                i = fin + 1;
                return id;
            }
            var ini = i;
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            return s[ini..i];
        }

        private static ReadOnlySpan<byte> RecortarAscii(ReadOnlySpan<byte> s)
        {
            var ini = 0;
            var fin = s.Length;
            while (ini < fin && s[ini] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') ini++;
            while (fin > ini && s[fin - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n') fin--;
            return s[ini..fin];
        }

        private static bool EsNull(ReadOnlySpan<byte> token) =>
            token.Length == 4
            && (token[0] is (byte)'N' or (byte)'n')
            && (token[1] is (byte)'U' or (byte)'u')
            && (token[2] is (byte)'L' or (byte)'l')
            && (token[3] is (byte)'L' or (byte)'l');
    }
}
