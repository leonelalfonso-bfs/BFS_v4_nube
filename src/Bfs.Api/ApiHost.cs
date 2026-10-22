using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations;
using Bfs.Integrations.Worms;
using Bfs.Scale;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Bfs.Api;

public static class ApiHost
{
    public const string DefaultUrl = "http://127.0.0.1:5080";

    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? DefaultUrl);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(DataPath(), "logs", "bfs-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        builder.Host.UseSerilog();

        var dbFile = Path.Combine(DataPath(), "bfs.db");
        builder.Services.AddDbContext<BfsDbContext>(o => o.UseSqlite($"Data Source={dbFile}"));
        builder.Services.AddHttpClient("worms");
        builder.Services.AddSingleton<ConfigurableScaleReader>();
        builder.Services.AddSingleton<IScaleReader>(sp => sp.GetRequiredService<ConfigurableScaleReader>());
        builder.Services.AddSingleton<IScaleController>(sp => sp.GetRequiredService<ConfigurableScaleReader>());
        builder.Services.AddHostedService<OutboxDispatcher>();
        builder.Services.AddSingleton<PasswordHasher<Usuario>>();

        var jwtKey = builder.Configuration["Jwt:Key"] ?? "bfs-dev-key-cambiar-en-produccion-32bytes!!";
        var signing = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey.PadRight(32, 'x')));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signing,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();

        Directory.CreateDirectory(Path.Combine(DataPath(), "logs"));
        Directory.CreateDirectory(Path.Combine(DataPath(), "branding"));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
            DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
            var scale = app.Services.GetRequiredService<IScaleController>();
            scale.Apply(ScaleSettingsFrom(LoadGlobalConfig(db)));
        }

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();

        MapAuth(app, signing);
        MapBranding(app);
        MapApi(app);

        app.MapFallbackToFile("index.html");
        return app;
    }

    public static string DataPath()
    {
        var programData = Environment.GetEnvironmentVariable("ProgramData") ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(programData, "bfs-4");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void MapAuth(WebApplication app, SymmetricSecurityKey signing)
    {
        app.MapPost("/api/auth/login", async (LoginRequest req, BfsDbContext db, PasswordHasher<Usuario> hasher) =>
        {
            var user = await db.Usuarios.FirstOrDefaultAsync(u => u.Username == req.Username && u.Activo);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
            {
                return Results.Unauthorized();
            }

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new("name", user.Nombre),
                new("username", user.Username),
                new(ClaimTypes.Role, user.Rol)
            };
            if (user.EmpresaId.HasValue)
            {
                claims.Add(new Claim("empresaId", user.EmpresaId.Value.ToString()));
            }

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(signing, SecurityAlgorithms.HmacSha256));

            return Results.Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                username = user.Username,
                nombre = user.Nombre,
                rol = user.Rol,
                empresaId = user.EmpresaId
            });
        });
    }

    private static void MapApi(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            username = user.FindFirstValue("username"),
            nombre = user.FindFirstValue("name"),
            rol = user.FindFirstValue(ClaimTypes.Role)
        }));

        api.MapGet("/health", () => Results.Ok(new { status = "ok", version = "4.0.0-spike" })).AllowAnonymous();

        api.MapGet("/scale/peso", (IScaleReader scale) => Results.Ok(scale.Current));

        api.MapGet("/scale/estado", (IScaleController scale) => Results.Ok(new
        {
            status = scale.Status,
            lastError = scale.LastError,
            settings = scale.Settings,
            puertos = scale.ListPorts()
        }));

        api.MapGet("/scale/puertos", (IScaleController scale) => Results.Ok(scale.ListPorts()));

        api.MapPost("/scale/aplicar", async (BfsDbContext db, IScaleController scale, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user)) return Results.Forbid();
            scale.Apply(ScaleSettingsFrom(LoadGlobalConfig(db)));
            return Results.Ok(new { status = scale.Status, lastError = scale.LastError, settings = scale.Settings });
        });

        api.MapGet("/campos", async (BfsDbContext db) =>
        {
            var json = await db.Configuraciones
                .Where(c => c.Clave == ConfigKeys.FieldsJson && c.EmpresaId == null)
                .Select(c => c.Valor)
                .FirstOrDefaultAsync();
            return Results.Ok(FieldCatalog.Parse(json));
        });

        api.MapGet("/empresas", async (BfsDbContext db) =>
            Results.Ok(await db.Empresas.OrderBy(e => e.Nombre).ToListAsync()));

        api.MapPost("/empresas", async (Empresa dto, BfsDbContext db, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user)) return Results.Forbid();
            dto.Id = Guid.NewGuid();
            db.Empresas.Add(dto);
            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });

        api.MapGet("/config", async (BfsDbContext db) =>
        {
            var items = await db.Configuraciones.Where(c => c.EmpresaId == null).ToListAsync();
            return Results.Ok(items.ToDictionary(c => c.Clave, c => c.Clave == ConfigKeys.WormsApiKey && c.Valor.Length > 0 ? "********" : c.Valor));
        });

        api.MapPut("/config", async (Dictionary<string, string> body, BfsDbContext db, IScaleController scale, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user)) return Results.Forbid();
            foreach (var (clave, valor) in body)
            {
                var row = await db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == clave && c.EmpresaId == null);
                if (clave == ConfigKeys.WormsApiKey && valor is "********" or "")
                {
                    continue;
                }
                if (row == null)
                {
                    db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = clave, Valor = valor });
                }
                else
                {
                    row.Valor = valor;
                }
            }
            await db.SaveChangesAsync();
            scale.Apply(ScaleSettingsFrom(LoadGlobalConfig(db)));
            return Results.Ok(new { status = scale.Status, lastError = scale.LastError });
        });

        api.MapGet("/catalogos/{tipo}", async (string tipo, BfsDbContext db) => tipo switch
        {
            "clientes" => Results.Ok(await db.Clientes.OrderBy(x => x.RazonSocial).ToListAsync()),
            "productos" => Results.Ok(await db.Productos.OrderBy(x => x.Nombre).ToListAsync()),
            "transportes" => Results.Ok(await db.Transportes.OrderBy(x => x.RazonSocial).ToListAsync()),
            _ => Results.NotFound()
        });

        api.MapPost("/catalogos/clientes", async (Cliente dto, BfsDbContext db) =>
        {
            dto.Id = Guid.NewGuid();
            db.Clientes.Add(dto);
            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });
        api.MapPost("/catalogos/productos", async (Producto dto, BfsDbContext db) =>
        {
            dto.Id = Guid.NewGuid();
            db.Productos.Add(dto);
            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });
        api.MapPost("/catalogos/transportes", async (Transporte dto, BfsDbContext db) =>
        {
            dto.Id = Guid.NewGuid();
            db.Transportes.Add(dto);
            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });

        api.MapGet("/usuarios", async (BfsDbContext db, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user) && !IsAdmin(user)) return Results.Forbid();
            var list = await (
                from u in db.Usuarios
                join e in db.Empresas on u.EmpresaId equals e.Id into gj
                from e in gj.DefaultIfEmpty()
                orderby u.Username
                select new
                {
                    u.Id,
                    u.Username,
                    u.Nombre,
                    u.Rol,
                    u.EmpresaId,
                    empresaNombre = e != null ? e.Nombre : null,
                    u.Activo
                }).ToListAsync();
            return Results.Ok(list);
        });

        api.MapPost("/usuarios", async (NuevoUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user) && !IsAdmin(user)) return Results.Forbid();
            if (dto.Rol == Roles.Vendor && !IsVendor(user)) return Results.Forbid();

            var entity = new Usuario
            {
                Id = Guid.NewGuid(),
                Username = dto.Username,
                Nombre = dto.Nombre,
                Rol = dto.Rol,
                EmpresaId = dto.EmpresaId,
                Activo = true
            };
            entity.PasswordHash = hasher.HashPassword(entity, dto.Password);
            db.Usuarios.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Username, entity.Rol });
        });

        api.MapPut("/usuarios/{id:guid}", async (Guid id, ActualizarUsuario dto, BfsDbContext db, PasswordHasher<Usuario> hasher, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user) && !IsAdmin(user)) return Results.Forbid();
            var entity = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
            if (entity == null) return Results.NotFound();
            if ((entity.Rol == Roles.Vendor || dto.Rol == Roles.Vendor) && !IsVendor(user)) return Results.Forbid();

            if (!string.IsNullOrWhiteSpace(dto.Nombre)) entity.Nombre = dto.Nombre;
            if (!string.IsNullOrWhiteSpace(dto.Rol)) entity.Rol = dto.Rol;
            entity.EmpresaId = dto.EmpresaId;
            if (dto.Activo.HasValue) entity.Activo = dto.Activo.Value;
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                entity.PasswordHash = hasher.HashPassword(entity, dto.Password);
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { entity.Id, entity.Username, entity.Nombre, entity.Rol, entity.EmpresaId, entity.Activo });
        });

        api.MapGet("/pesadas", async (Guid? empresaId, BfsDbContext db) =>
        {
            var q = db.Pesadas.AsQueryable();
            if (empresaId.HasValue) q = q.Where(p => p.EmpresaId == empresaId);
            return Results.Ok(await q.OrderByDescending(p => p.EntradaEn).Take(200).ToListAsync());
        });

        api.MapPost("/pesadas/entrada", async (EntradaRequest req, BfsDbContext db, IScaleReader scale, ClaimsPrincipal user) =>
        {
            var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == req.EmpresaId);
            if (empresa == null) return Results.BadRequest("Empresa inexistente");

            var configs = await db.Configuraciones.Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor);
            var ticket = empresa.ProximoTicket++;
            var pesada = new Pesada
            {
                Id = Guid.NewGuid(),
                EmpresaId = empresa.Id,
                TicketNumero = ticket.ToString(),
                Tipo = string.IsNullOrWhiteSpace(req.Tipo) ? configs.GetValueOrDefault(ConfigKeys.WormsTipoDefault, "INGRESO") : req.Tipo,
                Estado = "ENTRADA",
                PatenteChasis = req.PatenteChasis.Replace(" ", ""),
                PatenteAcoplado = req.PatenteAcoplado,
                Conductor = req.Conductor,
                Documento = req.Documento,
                Cliente = req.Cliente,
                Transporte = req.Transporte,
                Producto = req.Producto,
                Procedencia = req.Procedencia,
                Destino = req.Destino,
                Comprobante = req.Comprobante,
                Observaciones = req.Observaciones,
                Operador = user.FindFirstValue("username") ?? "",
                Balanza = configs.GetValueOrDefault(ConfigKeys.WormsBalanza, "1"),
                PesoEntrada = req.PesoEntrada ?? scale.Current.Kilogramos,
                TaraPallets = req.TaraPallets,
                EntradaEn = DateTimeOffset.Now
            };
            db.Pesadas.Add(pesada);
            EnqueueWorms(db, configs, pesada, "ENTRADA");
            await db.SaveChangesAsync();
            return Results.Ok(pesada);
        });

        api.MapPost("/pesadas/{id:guid}/salida", async (Guid id, SalidaRequest req, BfsDbContext db, IScaleReader scale) =>
        {
            var pesada = await db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
            if (pesada == null) return Results.NotFound();
            if (pesada.Estado == "SALIDA") return Results.BadRequest("Ya tiene salida");

            var configs = await db.Configuraciones.Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor);
            pesada.PesoSalida = req.PesoSalida ?? scale.Current.Kilogramos;
            pesada.TaraPallets = req.TaraPallets ?? pesada.TaraPallets;
            pesada.SalidaEn = DateTimeOffset.Now;
            pesada.Estado = "SALIDA";
            var calc = NetoCalculator.Calcular(pesada.PesoEntrada, pesada.PesoSalida.Value, pesada.TaraPallets);
            pesada.Bruto = calc.Bruto;
            pesada.Tara = calc.Tara;
            pesada.Neto = calc.Neto;
            EnqueueWorms(db, configs, pesada, "SALIDA");
            await db.SaveChangesAsync();
            return Results.Ok(pesada);
        });
    }

    private static void MapBranding(WebApplication app)
    {
        app.MapGet("/api/branding", async (BfsDbContext db) =>
        {
            var c = await db.Configuraciones.Where(x => x.EmpresaId == null).ToDictionaryAsync(x => x.Clave, x => x.Valor);
            var logo = c.GetValueOrDefault(ConfigKeys.LogoFile, "");
            return Results.Ok(new
            {
                clienteNombre = c.GetValueOrDefault(ConfigKeys.ClienteNombre, ""),
                ticketTitulo = c.GetValueOrDefault(ConfigKeys.TicketTitulo, "Comprobante de pesaje"),
                ticketPie = c.GetValueOrDefault(ConfigKeys.TicketPie, ""),
                logoUrl = string.IsNullOrWhiteSpace(logo) ? null : "/api/branding/logo"
            });
        });

        app.MapGet("/api/branding/logo", async (BfsDbContext db) =>
        {
            var logo = await db.Configuraciones
                .Where(c => c.Clave == ConfigKeys.LogoFile && c.EmpresaId == null)
                .Select(c => c.Valor)
                .FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(logo)) return Results.NotFound();
            var path = Path.Combine(DataPath(), "branding", Path.GetFileName(logo));
            if (!File.Exists(path)) return Results.NotFound();
            var content = Path.GetExtension(logo).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
            return Results.File(path, content);
        });

        app.MapPost("/api/branding/logo", async (HttpRequest request, BfsDbContext db, ClaimsPrincipal user) =>
        {
            if (!IsVendor(user)) return Results.Forbid();
            if (!request.HasFormContentType) return Results.BadRequest("Archivo requerido");
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return Results.BadRequest("Archivo requerido");
            if (file.Length > 2 * 1024 * 1024) return Results.BadRequest("El logo no puede superar 2 MB");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".svg"))
            {
                return Results.BadRequest("Usá PNG, JPG, WEBP o SVG");
            }

            var dir = Path.Combine(DataPath(), "branding");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "logo.*"))
            {
                try { File.Delete(old); } catch { /* ignore locked files */ }
            }

            var name = "logo" + ext;
            await using (var stream = File.Create(Path.Combine(dir, name)))
            {
                await file.CopyToAsync(stream);
            }

            var row = await db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == ConfigKeys.LogoFile && c.EmpresaId == null);
            if (row == null)
            {
                db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = ConfigKeys.LogoFile, Valor = name });
            }
            else
            {
                row.Valor = name;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { logoUrl = "/api/branding/logo" });
        }).RequireAuthorization().DisableAntiforgery();
    }

    private static Dictionary<string, string> LoadGlobalConfig(BfsDbContext db) =>
        db.Configuraciones.AsNoTracking().Where(c => c.EmpresaId == null).ToDictionary(c => c.Clave, c => c.Valor);

    private static ScaleSettings ScaleSettingsFrom(Dictionary<string, string> c) =>
        new(
            c.GetValueOrDefault(ConfigKeys.ScaleMode, "sim"),
            c.GetValueOrDefault(ConfigKeys.ScalePort, ""),
            int.TryParse(c.GetValueOrDefault(ConfigKeys.ScaleBaud, "9600"), out var baud) ? baud : 9600,
            int.TryParse(c.GetValueOrDefault(ConfigKeys.ScaleDataBits, "8"), out var bits) ? bits : 8,
            int.TryParse(c.GetValueOrDefault(ConfigKeys.ScaleStopBits, "1"), out var stop) ? stop : 1,
            c.GetValueOrDefault(ConfigKeys.ScaleParity, "None"),
            c.GetValueOrDefault(ConfigKeys.ScaleProtocol, "Line"),
            c.GetValueOrDefault(ConfigKeys.ScaleExtract, ""));

    private static void EnqueueWorms(BfsDbContext db, Dictionary<string, string> configs, Pesada pesada, string evento)
    {
        if (configs.GetValueOrDefault(ConfigKeys.WormsEnabled) != "true") return;
        var payload = WormsMapper.FromPesada(pesada, evento, configs.GetValueOrDefault(ConfigKeys.WormsBalanza, "1"));
        db.Outbox.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Destino = "worms",
            PesadaId = pesada.Id,
            TicketNumero = pesada.TicketNumero,
            Evento = evento,
            PayloadJson = JsonSerializer.Serialize(payload),
            Estado = "pendiente",
            CreadoEn = DateTimeOffset.Now,
            ProximoIntento = DateTimeOffset.Now
        });
    }

    private static bool IsVendor(ClaimsPrincipal user) => user.IsInRole(Roles.Vendor);
    private static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}

public record LoginRequest(string Username, string Password);
public record NuevoUsuario(string Username, string Password, string Nombre, string Rol, Guid? EmpresaId);
public record ActualizarUsuario(string? Nombre, string? Rol, Guid? EmpresaId, bool? Activo, string? Password);
public record EntradaRequest(
    Guid EmpresaId,
    string PatenteChasis,
    string? PatenteAcoplado,
    string? Conductor,
    string? Documento,
    string? Cliente,
    string? Transporte,
    string? Producto,
    string? Procedencia,
    string? Destino,
    string? Comprobante,
    string? Observaciones,
    string? Tipo,
    int? PesoEntrada,
    int TaraPallets);
public record SalidaRequest(int? PesoSalida, int? TaraPallets);
