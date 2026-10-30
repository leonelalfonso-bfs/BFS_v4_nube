using System.Globalization;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Infrastructure.Importacion;

public record ConteoImportacion(int Importados, int Omitidos);

public record ResultadoImportacion(IReadOnlyDictionary<string, ConteoImportacion> Conteos, IReadOnlyList<string> Avisos);

/// <summary>
/// Pasa una base <c>truckscale.db</c> de la 3.x a la 4.0. La base vieja se abre en solo lectura y no se toca.
/// Cada cliente tiene una variante distinta de la 3.x (columnas agregadas con los años), así que cada columna
/// se lee solo si existe. Lo que ya está en la 4.0 (mismo nombre, mismo ticket) se omite: se puede correr dos veces.
/// </summary>
public class Importador3x
{
    public const string OperadorImportado = "importado 3.x";

    private static readonly string[] FormatosFecha =
        ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"];

    private readonly BfsDbContext _db;
    private readonly PasswordHasher<Usuario> _hasher;
    private readonly TimeZoneInfo _zona;
    private readonly string _carpetaBranding;
    private readonly Dictionary<string, ConteoImportacion> _conteos = new();
    private readonly List<string> _avisos = [];

    public Importador3x(BfsDbContext db, PasswordHasher<Usuario> hasher, string zonaHoraria, string carpetaBranding)
    {
        _db = db;
        _hasher = hasher;
        _carpetaBranding = carpetaBranding;
        try { _zona = TimeZoneInfo.FindSystemTimeZoneById(zonaHoraria); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { _zona = TimeZoneInfo.Local; }
    }

    /// <summary>Lo que hay en la base vieja, para mostrar antes de importar.</summary>
    public static Dictionary<string, long> Contar(string truckscaleDb)
    {
        using var conn = Abrir(truckscaleDb);
        var tablas = Tablas(conn);
        return new[] { "weighings", "clients", "transports", "productos", "users", "balanzas" }
            .Where(tablas.Contains)
            .ToDictionary(t => t, t => Escalar(conn, $"SELECT COUNT(*) FROM \"{t}\""));
    }

    public async Task<ResultadoImportacion> ImportarAsync(string truckscaleDb, Guid empresaId)
    {
        using var conn = Abrir(truckscaleDb);
        var tablas = Tablas(conn);
        if (!tablas.Contains("weighings")) throw new InvalidOperationException("El archivo no es una base de la 3.x (no tiene la tabla weighings)");

        var empresa = await _db.Empresas.FirstAsync(e => e.Id == empresaId);
        await using var tx = await _db.Database.BeginTransactionAsync();

        if (tablas.Contains("client_config")) await Empresa(conn, empresa, Path.GetDirectoryName(Path.GetFullPath(truckscaleDb))!);
        if (tablas.Contains("system_config")) await Sistema(conn);
        if (tablas.Contains("aduana_config")) await Aduana(conn);

        var clientes = await Catalogo(conn, tablas, "clients", "Clientes", _db.Clientes, c => c.RazonSocial, f => new Cliente
        {
            RazonSocial = Str(f, "razon_social") ?? "", Cuit = CuitTolerante(Str(f, "cuit"), "Cliente " + Str(f, "razon_social"))
        });
        await Catalogo(conn, tablas, "transports", "Transportes", _db.Transportes, t => t.RazonSocial, f => new Transporte
        {
            RazonSocial = Str(f, "razon_social") ?? "", Cuit = CuitTolerante(Str(f, "cuit"), "Transporte " + Str(f, "razon_social")),
            EsAta = Bool(f, "es_ata"), CodigoAta = Str(f, "codigo_ata")
        });
        await Catalogo(conn, tablas, "productos", "Productos", _db.Productos, p => p.Nombre, f => new Producto
        {
            Nombre = Str(f, "nombre") ?? "", Codigo = Str(f, "codigo")
        });
        await Catalogo(conn, tablas, "exportadores", "Exportadores", _db.Exportadores, x => x.RazonSocial, f => new Exportador
        {
            RazonSocial = Str(f, "razon_social") ?? "", Cuit = CuitTolerante(Str(f, "cuit"), "Exportador " + Str(f, "razon_social"))
        });
        await Catalogo(conn, tablas, "taras", "Taras", _db.Taras, t => t.Identificador, f => new TaraConocida
        {
            Identificador = Patente.Normalizar(Str(f, "carro_id")), Kilogramos = Int(f, "tara") ?? 0,
            Descripcion = Str(f, "descripcion"), TomadaEn = Fecha(Str(f, "fecha")) ?? DateTimeOffset.UtcNow
        }, f => (Int(f, "tara") ?? 0) > 0 && Patente.Normalizar(Str(f, "carro_id")).Length > 0);
        await Catalogo(conn, tablas, "balanza_modelos", "Modelos de balanza", _db.ModelosBalanza, m => m.Nombre, f => new ModeloBalanza
        {
            Nombre = Str(f, "modelo") ?? "", Baudios = Int(f, "baudrate") ?? 9600, DataBits = Int(f, "databits") ?? 8,
            Paridad = Paridad(Str(f, "parity")), StopBits = Int(f, "stopbits") ?? 1, Protocolo = Protocolo(Str(f, "protocolo")),
            Extraccion = Str(f, "regex")
        });

        if (tablas.Contains("balanzas")) await Balanzas(conn);
        if (tablas.Contains("users")) await Usuarios(conn, clientes);
        await Pesadas(conn, empresa, clientes);
        if (tablas.Contains("weighing_data_cache")) await Vehiculos(conn);

        await tx.CommitAsync();
        return new ResultadoImportacion(_conteos, _avisos);
    }

    private async Task Empresa(SqliteConnection conn, Empresa empresa, string carpetaBase)
    {
        var f = Filas(conn, "client_config").FirstOrDefault();
        if (f == null) return;

        var razon = Str(f, "razon_social");
        if (razon != null && !await _db.Empresas.AnyAsync(e => e.Id != empresa.Id && e.Nombre.ToLower() == razon.ToLower()))
        {
            empresa.Nombre = razon;
        }
        empresa.Cuit = CuitTolerante(Str(f, "cuit"), "La empresa") ?? empresa.Cuit;
        empresa.Direccion = Str(f, "direccion") ?? empresa.Direccion;
        empresa.Localidad = Str(f, "localidad") ?? empresa.Localidad;
        empresa.Provincia = Str(f, "provincia") ?? empresa.Provincia;
        empresa.Telefono = Str(f, "telefono") ?? empresa.Telefono;
        empresa.Email = Str(f, "mail") ?? empresa.Email;

        var config = new Dictionary<string, string>
        {
            [ConfigKeys.ClienteNombre] = empresa.Nombre,
            [ConfigKeys.TaraConocida] = Bool(f, "enable_tara_conocida") ? "true" : "false",
            [ConfigKeys.IngresoManual] = Tiene(f, "enable_ingreso_manual") && !Bool(f, "enable_ingreso_manual") ? "false" : "true",
            [ConfigKeys.Termica] = Bool(f, "enable_thermal_printer") ? "true" : "false"
        };
        if (Str(f, "thermal_printer_name") is { } impresora) config[ConfigKeys.TermicaImpresora] = impresora;
        if (Str(f, "thermal_paper_width") is { } ancho) config[ConfigKeys.TermicaAncho] = ancho.StartsWith("58") ? "58" : "80";
        if (Str(f, "clave_manual") is { } clave) config[ConfigKeys.ClaveManualHash] = _hasher.HashPassword(new Usuario(), clave);

        // El logo: la 3.x guardaba la ruta absoluta; si el archivo sigue ahí (o al lado de la base) se copia.
        if (Str(f, "logo_path") is { } logo)
        {
            var origen = new[] { logo, Path.Combine(carpetaBase, Path.GetFileName(logo)) }.FirstOrDefault(File.Exists);
            var ext = Path.GetExtension(logo).ToLowerInvariant();
            if (origen != null && ext is ".png" or ".jpg" or ".jpeg")
            {
                Directory.CreateDirectory(_carpetaBranding);
                File.Copy(origen, Path.Combine(_carpetaBranding, "logo" + ext), overwrite: true);
                config[ConfigKeys.LogoFile] = "logo" + ext;
            }
            else
            {
                _avisos.Add($"No se encontró el logo ({logo}): cargalo desde Configuración → Ticket y logo");
            }
        }

        await GuardarConfig(config);
        Contar("Empresa", 1, 0);
    }

    private async Task Sistema(SqliteConnection conn)
    {
        var modo = Filas(conn, "system_config").FirstOrDefault(f => Str(f, "config_key") == "operation_mode");
        if (modo != null && Str(modo, "config_value") == "Aduana")
        {
            await GuardarConfig(new() { [ConfigKeys.Aduana] = "true" });
        }
    }

    private async Task Aduana(SqliteConnection conn)
    {
        var f = Filas(conn, "aduana_config").FirstOrDefault();
        if (f == null) return;
        string Dia(string col) => Fecha(Str(f, col)) is { } d ? TimeZoneInfo.ConvertTime(d, _zona).ToString("yyyy-MM-dd") : "";
        await GuardarConfig(new()
        {
            [ConfigKeys.AduanaCodigo] = Str(f, "codigo_aduana") ?? "",
            [ConfigKeys.AduanaLot] = Str(f, "codigo_lot") ?? "",
            [ConfigKeys.AduanaCertificado1] = Str(f, "certificado_numero") ?? "",
            [ConfigKeys.AduanaCertificado1Vence] = Dia("certificado_vencimiento"),
            [ConfigKeys.AduanaCertificado2] = Str(f, "certificado_numero_bal2") ?? "",
            [ConfigKeys.AduanaCertificado2Vence] = Dia("certificado_vencimiento_bal2")
        });
    }

    /// <summary>Alta de un catálogo por nombre (sin distinguir mayúsculas). Devuelve nombre → id de lo que quedó en la 4.0.</summary>
    private async Task<Dictionary<string, Guid>> Catalogo<T>(SqliteConnection conn, HashSet<string> tablas, string tabla, string titulo,
        DbSet<T> set, Func<T, string> nombreDe, Func<Dictionary<string, object?>, T> crear,
        Func<Dictionary<string, object?>, bool>? valida = null)
        where T : EntidadSync
    {
        var existentes = (await set.IgnoreQueryFilters().ToListAsync())
            .GroupBy(x => nombreDe(x).Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
        if (!tablas.Contains(tabla)) return existentes;

        int importados = 0, omitidos = 0;
        foreach (var f in Filas(conn, tabla))
        {
            if (valida != null && !valida(f))
            {
                omitidos++;
                continue;
            }
            var entidad = crear(f);
            var clave = nombreDe(entidad).Trim().ToLowerInvariant();
            if (clave.Length == 0 || existentes.ContainsKey(clave))
            {
                omitidos++;
                continue;
            }
            entidad.Id = Guid.NewGuid();
            set.Add(entidad);
            existentes[clave] = entidad.Id;
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar(titulo, importados, omitidos);
        return existentes;
    }

    private async Task Balanzas(SqliteConnection conn)
    {
        int importadas = 0, omitidas = 0;
        var orden = await _db.Balanzas.MaxAsync(b => (int?)b.Orden) ?? 0;
        foreach (var f in Filas(conn, "balanzas"))
        {
            var nombre = Str(f, "nombre");
            if (nombre == null)
            {
                omitidas++;
                continue;
            }
            // La "Balanza 1" simulada que trae una base nueva se reemplaza por la real de la 3.x.
            var b = await _db.Balanzas.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Nombre.ToLower() == nombre.ToLower());
            if (b != null && !(b.Modo == ModosBalanza.Simulada && string.IsNullOrEmpty(b.Puerto)))
            {
                omitidas++;
                continue;
            }
            if (b == null)
            {
                b = new Balanza { Id = Guid.NewGuid(), Orden = ++orden };
                _db.Balanzas.Add(b);
            }
            var puerto = Str(f, "puerto")?.ToUpperInvariant() ?? "";
            b.Nombre = nombre;
            b.Eliminado = false;
            b.Activa = true;
            b.Modo = puerto.Length > 0 ? ModosBalanza.Serie : ModosBalanza.Simulada;
            b.Puerto = puerto;
            b.Modelo = Str(f, "modelo");
            b.Baudios = Int(f, "baudrate") ?? 9600;
            b.DataBits = Int(f, "databits") ?? 8;
            b.Paridad = Paridad(Str(f, "parity"));
            b.StopBits = Int(f, "stopbits") ?? 1;
            b.Protocolo = Protocolo(Str(f, "protocolo"));
            b.Extraccion = Str(f, "regex");
            importadas++;
        }
        await _db.SaveChangesAsync();
        Contar("Balanzas", importadas, omitidas);
    }

    private async Task Usuarios(SqliteConnection conn, Dictionary<string, Guid> clientes)
    {
        int importados = 0, omitidos = 0;
        foreach (var f in Filas(conn, "users"))
        {
            var login = Usuario.NormalizarEmail(Str(f, "username"));
            if (login.Length == 0 || await _db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == login))
            {
                omitidos++;
                continue;
            }

            var u = new Usuario { Id = Guid.NewGuid(), Email = login, Nombre = Str(f, "username")!, Activo = true, Rol = Roles.DesdePrivilegio3x(Int(f, "privilege_level") ?? 0) };
            if (Str(f, "client_razon") is { } razon)
            {
                // Usuario de portal: su cliente tiene que existir en el catálogo.
                if (!clientes.TryGetValue(razon.Trim().ToLowerInvariant(), out var clienteId))
                {
                    var nuevo = new Cliente { Id = Guid.NewGuid(), RazonSocial = razon.Trim() };
                    _db.Clientes.Add(nuevo);
                    clientes[razon.Trim().ToLowerInvariant()] = clienteId = nuevo.Id;
                }
                u.Rol = Roles.Cliente;
                u.ClienteId = clienteId;
            }

            var clave = Str(f, "password");
            if (clave == null)
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
            _avisos.Add("Los usuarios de la 3.x entran con su mismo nombre y clave; conviene cambiarles el nombre por su email desde Usuarios.");
        }
    }

    private async Task Pesadas(SqliteConnection conn, Empresa empresa, Dictionary<string, Guid> clientes)
    {
        // Ya importada = mismo ticket y misma fecha de entrada, en cualquier empresa (el nombre de la empresa cambia al importar).
        var existentes = (await _db.Pesadas.IgnoreQueryFilters().Select(p => new { p.TicketNumero, p.EntradaEn }).ToListAsync())
            .Select(x => (x.TicketNumero, x.EntradaEn.UtcTicks)).ToHashSet();
        int importadas = 0, omitidas = 0, conFotos = 0, sinFecha = 0, maximo = 0;

        foreach (var f in Filas(conn, "weighings"))
        {
            var ticket = Str(f, "ticket_numero") ?? Str(f, "id")!;
            var entrada = Fecha(Str(f, "fecha"));
            if (entrada == null)
            {
                sinFecha++;
                entrada = DateTimeOffset.UnixEpoch;
            }
            if (!existentes.Add((ticket, entrada.Value.UtcTicks)))
            {
                omitidas++;
                continue;
            }
            var pendiente = (Int(f, "salida_pendiente") ?? 0) == 1;
            var tipo = Str(f, "tipo_operacion");
            var p = new Pesada
            {
                Id = Guid.NewGuid(),
                EmpresaId = empresa.Id,
                TicketNumero = ticket,
                Tipo = Str(f, "tipo_movimiento")?.ToUpperInvariant() is "EGRESO" ? "EGRESO" : "INGRESO",
                Estado = pendiente ? EstadosPesada.Entrada : EstadosPesada.Salida,
                Modo = tipo == "Tara Conocida" ? ModosPesaje.TaraConocida : ModosPesaje.Normal,
                TipoOperacion = tipo == "Aduana" ? TiposOperacion.Aduana : TiposOperacion.Normal,
                PatenteChasis = Patente.Normalizar(Str(f, "patente_chasis") ?? Str(f, "carro_id")),
                PatenteAcoplado = Str(f, "patente_acoplado") is { } acoplado ? Patente.Normalizar(acoplado) : null,
                Conductor = Str(f, "nombre_chofer"),
                Documento = Str(f, "dni_chofer"),
                Cliente = Str(f, "client_razon"),
                Transporte = Str(f, "transport_razon"),
                Producto = Str(f, "producto") ?? Str(f, "mercaderia"),
                Procedencia = Str(f, "procedencia"),
                Destino = Str(f, "destino"),
                Comprobante = Str(f, "remito"),
                Observaciones = Str(f, "observaciones"),
                Operador = Str(f, "usuario_entrada") ?? OperadorImportado,
                OperadorSalida = pendiente ? null : Str(f, "usuario_salida") ?? OperadorImportado,
                PesoEntrada = Int(f, "peso_entrada") ?? 0,
                TaraPallets = Int(f, "tara_pallets") ?? 0,
                EntradaEn = entrada.Value,
                CreadoEn = entrada.Value
            };
            if (p.Cliente != null && clientes.TryGetValue(p.Cliente.Trim().ToLowerInvariant(), out var clienteId)) p.ClienteId = clienteId;

            if (!pendiente)
            {
                p.PesoSalida = Int(f, "peso_salida");
                p.SalidaEn = Fecha(Str(f, "fecha_salida")) ?? entrada;
                p.Bruto = Int(f, "bruto");
                p.Tara = Int(f, "tara");
                p.Neto = Int(f, "neto");
                if (p.Neto == null && p.PesoSalida.HasValue)
                {
                    (p.Bruto, p.Tara, p.Neto) = NetoCalculator.Calcular(p.PesoEntrada, p.PesoSalida.Value, p.TaraPallets);
                }
            }

            if (p.TipoOperacion == TiposOperacion.Aduana)
            {
                p.Aduana = new DatosAduana
                {
                    Escenario = Str(f, "aduana_escenario") ?? "",
                    Exportador = Str(f, "exportador_razon_social"),
                    ExportadorCuit = Str(f, "exportador_cuit"),
                    OperacionId = Str(f, "aduana_operacion_id"),
                    ChoferTipoDocumento = Str(f, "chofer_tipo_documento"),
                    ChoferNacionalidad = Str(f, "chofer_nacionalidad"),
                    ContenedorId = Str(f, "contenedor_identificador"),
                    ContenedorTara = Int(f, "contenedor_peso_tara") ?? 0,
                    ContenedorBruto = Int(f, "contenedor_peso_bruto"),
                    ContenedorNeto = Int(f, "contenedor_peso_neto"),
                    BultoId = Str(f, "bulto_identificador"),
                    BultoBruto = Int(f, "bulto_peso_bruto"),
                    BultoTara = Int(f, "bulto_peso_tara")
                };
            }

            if (new[] { "foto_entrada", "foto_salida", "foto_entrada2", "foto_salida2" }.Any(c => Str(f, c) != null)) conFotos++;
            if (int.TryParse(ticket, out var numero)) maximo = Math.Max(maximo, numero);

            _db.Pesadas.Add(p);
            importadas++;
            if (importadas % 500 == 0) await _db.SaveChangesAsync();
        }
        await _db.SaveChangesAsync();
        Contar("Pesadas", importadas, omitidas);

        // Los tickets nuevos siguen después del último de la 3.x (puesto 1: 01-000038 si el último era el 37).
        if (maximo > 0)
        {
            var contador = await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == empresa.Id && c.Puesto == 1);
            if (contador == null)
            {
                contador = new ContadorTicket { Id = Guid.NewGuid(), EmpresaId = empresa.Id, Puesto = 1, Proximo = 1 };
                _db.ContadoresTicket.Add(contador);
            }
            contador.Proximo = Math.Max(contador.Proximo, maximo + 1);
            empresa.ProximoTicket = Math.Max(empresa.ProximoTicket, maximo + 1);
            await _db.SaveChangesAsync();
        }
        if (conFotos > 0) _avisos.Add($"{conFotos} pesadas tenían fotos: la 4.0 todavía no las importa (quedan en la carpeta de la 3.x).");
        if (sinFecha > 0) _avisos.Add($"{sinFecha} pesadas no tenían una fecha legible y quedaron con fecha 01/01/1970.");
    }

    private async Task Vehiculos(SqliteConnection conn)
    {
        var existentes = (await _db.DatosVehiculos.IgnoreQueryFilters().Select(d => d.PatenteChasis).ToListAsync()).ToHashSet();
        int importados = 0, omitidos = 0;
        foreach (var f in Filas(conn, "weighing_data_cache"))
        {
            var patente = Patente.Normalizar(Str(f, "patente_chasis"));
            if (patente.Length == 0 || !existentes.Add(patente))
            {
                omitidos++;
                continue;
            }
            _db.DatosVehiculos.Add(new DatosVehiculo
            {
                Id = Guid.NewGuid(),
                PatenteChasis = patente,
                PatenteAcoplado = Str(f, "patente_acoplado") is { } a ? Patente.Normalizar(a) : null,
                Conductor = Str(f, "nombre_chofer"),
                Documento = Str(f, "dni_chofer"),
                TipoDocumento = Str(f, "chofer_tipo_documento"),
                Nacionalidad = Str(f, "chofer_nacionalidad"),
                Transporte = Str(f, "transport_razon")
            });
            importados++;
        }
        await _db.SaveChangesAsync();
        Contar("Datos de vehículos", importados, omitidos);
    }

    private async Task GuardarConfig(Dictionary<string, string> valores)
    {
        foreach (var (clave, valor) in valores)
        {
            var row = await _db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == clave && c.EmpresaId == null);
            if (row == null) _db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = clave, Valor = valor });
            else row.Valor = valor;
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Un CUIT con dígito verificador mal se conserva igual (es un dato del cliente), pero se avisa.</summary>
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
        if (texto == null) return null;
        if (!DateTime.TryParseExact(texto.Trim(), FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) return null;
        return new DateTimeOffset(local, _zona.GetUtcOffset(local)).ToUniversalTime();
    }

    private static string Paridad(string? p) => p?.Trim() switch
    {
        "Even" or "Odd" or "Mark" or "Space" => p.Trim(),
        _ => "None"
    };

    private static string Protocolo(string? p) =>
        string.Equals(p?.Trim(), ProtocolosBalanza.Line, StringComparison.OrdinalIgnoreCase) ? ProtocolosBalanza.Line : ProtocolosBalanza.Stx;

    private static bool Tiene(Dictionary<string, object?> f, string col) => f.ContainsKey(col) && f[col] != null;

    private static string? Str(Dictionary<string, object?> f, string col) =>
        f.TryGetValue(col, out var v) && v != null && Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() is { Length: > 0 } s ? s : null;

    private static int? Int(Dictionary<string, object?> f, string col) => f.TryGetValue(col, out var v) ? v switch
    {
        long l => (int)l,
        double d => (int)Math.Round(d, MidpointRounding.AwayFromZero),
        string s when double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => (int)Math.Round(d, MidpointRounding.AwayFromZero),
        _ => null
    } : null;

    private static bool Bool(Dictionary<string, object?> f, string col) => f.TryGetValue(col, out var v) && v switch
    {
        long l => l != 0,
        string s => s.Trim() is "1" or "true" or "True" or "S" or "Si" or "Sí",
        _ => false
    };

    private static SqliteConnection Abrir(string archivo)
    {
        if (!File.Exists(archivo)) throw new FileNotFoundException("No se encontró la base de la 3.x", archivo);
        var conn = new SqliteConnection($"Data Source={archivo};Mode=ReadOnly;Pooling=False");
        conn.Open();
        return conn;
    }

    private static HashSet<string> Tablas(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        using var r = cmd.ExecuteReader();
        var tablas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (r.Read()) tablas.Add(r.GetString(0));
        return tablas;
    }

    private static long Escalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static List<Dictionary<string, object?>> Filas(SqliteConnection conn, string tabla)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{tabla}\"";
        using var r = cmd.ExecuteReader();
        var filas = new List<Dictionary<string, object?>>();
        while (r.Read())
        {
            var fila = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < r.FieldCount; i++) fila[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            filas.Add(fila);
        }
        return filas;
    }
}
