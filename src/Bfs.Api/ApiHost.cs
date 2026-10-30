using System.Text;
using Bfs.Api.Auth;
using Bfs.Api.Branding;
using Bfs.Api.Catalogos;
using Bfs.Api.Config;
using Bfs.Api.Documentos;
using Bfs.Api.Licencias;
using Bfs.Api.Nube;
using Bfs.Api.Pesadas;
using Bfs.Api.Planta;
using Bfs.Api.Reportes;
using Bfs.Api.Sistema;
using Bfs.Api.Balanzas;
using Bfs.Api.Usuarios;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Documents;
using Bfs.Infrastructure.Data;
using Bfs.Integrations;
using Bfs.Scale;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Bfs.Api;

public static class ApiHost
{
    public const string DefaultUrl = "http://127.0.0.1:5080";

    public static WebApplication Create(string[] args)
    {
        // La raíz es la carpeta del programa (no la carpeta desde donde lo abrieron): ahí están appsettings.json y las DLL.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? DefaultUrl);
        builder.Environment.WebRootFileProvider = new ManifestEmbeddedFileProvider(typeof(ApiHost).Assembly, "wwwroot");

        var options = builder.Configuration.GetSection(BfsOptions.Seccion).Get<BfsOptions>() ?? new BfsOptions();
        var paths = new BfsPaths(options.DataPath);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(paths);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(paths.Logs, "bfs-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
        builder.Host.UseSerilog();

        AddDatosYBalanza(builder, options, paths);

        builder.Services.AddHttpClient("worms");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<TenantActual>();
        builder.Services.AddSingleton<PasswordHasher<Usuario>>();
        builder.Services.AddScoped<ConfigService>();
        builder.Services.AddScoped<PesajeService>();
        builder.Services.AddScoped<DocumentosService>();
        builder.Services.AddScoped<ReportesService>();
        builder.Services.AddScoped<SistemaService>();
        builder.Services.AddSingleton(_ => new LicenciaService(options, paths, builder.Configuration, builder.Environment));

        var signing = ClaveJwt(builder.Configuration["Jwt:Key"], paths);
        builder.Services.AddSingleton(signing);

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                // Sin esto "email" llega renombrado al esquema largo de Microsoft y user.Email() queda vacío.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signing,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddBfsAutorizacion();
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();

        if (options.Modo == ModoDespliegue.Nube)
        {
            NubeHost.InicializarAsync(app.Services, app.Logger).GetAwaiter().GetResult();
        }
        else
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
            DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
            BalanzaEndpoints.AplicarAsync(db, app.Services.GetRequiredService<IBalanzas>()).GetAwaiter().GetResult();
        }

        app.UseCors();
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = app.Environment.WebRootFileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = app.Environment.WebRootFileProvider,
            // Que el navegador (y el WebView2 de la portería) revalide siempre: al actualizar el sistema
            // no tiene que quedar corriendo el app.js de la versión anterior.
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
        });
        app.UseAuthentication();
        if (options.Modo == ModoDespliegue.Nube) app.UseMiddleware<EstadoTenantMiddleware>();
        app.UseAuthorization();

        app.MapAuthEndpoints(options.Modo);
        app.MapBrandingEndpoints();

        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/health", () => Results.Ok(new { status = "ok", version = "4.0.0-spike", modo = options.Modo.ToString() })).AllowAnonymous();
        api.MapBalanzaEndpoints();
        api.MapConfigEndpoints();
        api.MapCatalogoEndpoints();
        api.MapPlantaEndpoints();
        api.MapUsuarioEndpoints();
        api.MapPesadaEndpoints();
        api.MapLicenciaEndpoints();
        api.MapDocumentoEndpoints();
        api.MapReporteEndpoints();
        api.MapSistemaEndpoints();
        api.MapImportacionEndpoints();
        if (options.Modo == ModoDespliegue.Nube) api.MapSuperadminEndpoints();

        // Una ruta de la API que no existe es 404; cualquier otra ruta es la aplicación web.
        app.MapFallback("/api/{**resto}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>
    /// Clave para firmar las sesiones. Si no viene por configuración, cada instalación genera la suya la primera vez
    /// (antes había una fija en el código, que además está en el repositorio público: cualquiera podía falsificar un login).
    /// </summary>
    private static SymmetricSecurityKey ClaveJwt(string? configurada, BfsPaths paths)
    {
        if (!string.IsNullOrWhiteSpace(configurada) && configurada.Length >= 32)
        {
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configurada));
        }

        var archivo = Path.Combine(paths.Root, "jwt.key");
        if (!File.Exists(archivo))
        {
            File.WriteAllText(archivo, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));
        }
        return new SymmetricSecurityKey(Convert.FromBase64String(File.ReadAllText(archivo).Trim()));
    }

    private static void AddDatosYBalanza(WebApplicationBuilder builder, BfsOptions options, BfsPaths paths)
    {
        if (options.Modo == ModoDespliegue.Nube)
        {
            // Una base por cliente; la integración con WORMS desde la nube queda para la Fase 8.
            builder.AddNube(paths);
            return;
        }

        if (ArchivosBase.AplicarRestauracionPendiente(paths.Restaurar, paths.BaseSqlite, paths.Backups) is { } anterior)
        {
            Log.Warning("Se restauró un backup. La base anterior quedó en {Anterior}", anterior);
        }

        var respaldo = SpikeDbRespaldo.ApartarSiNoTieneMigraciones(paths.BaseSqlite);
        if (respaldo != null)
        {
            Log.Warning("La base del spike no tenía migraciones; se apartó en {Respaldo} y se crea una nueva", respaldo);
        }

        builder.Services.AddSingleton(new NodoInfo(paths.NodoId()));
        builder.Services.AddBfsDatabase(ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(paths.BaseSqlite));

        builder.Services.AddSingleton<IImpresoraTermica, ImpresoraWindows>();
        builder.Services.AddScoped<IIndiceLogins, IndiceLoginsNodo>();
        builder.Services.AddHostedService<OutboxDispatcher>();
        builder.Services.AddHostedService<RespaldoAutomatico>();
        builder.Services.AddSingleton<IPuertoSerieFactory, PuertoSerieFactory>();
        builder.Services.AddSingleton<IBalanzas>(sp => new BalanzaManager(
            sp.GetRequiredService<IPuertoSerieFactory>(), sp.GetRequiredService<ILogger<BalanzaManager>>()));
    }
}
