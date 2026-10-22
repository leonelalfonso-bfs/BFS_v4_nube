using System.Text;
using Bfs.Api.Auth;
using Bfs.Api.Branding;
using Bfs.Api.Catalogos;
using Bfs.Api.Config;
using Bfs.Api.Licencias;
using Bfs.Api.Pesadas;
using Bfs.Api.Planta;
using Bfs.Api.Balanza;
using Bfs.Api.Usuarios;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations;
using Bfs.Scale;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
        builder.Services.AddHostedService<OutboxDispatcher>();
        builder.Services.AddSingleton<PasswordHasher<Usuario>>();
        builder.Services.AddScoped<ConfigService>();
        builder.Services.AddScoped<PesajeService>();
        builder.Services.AddSingleton(_ => new LicenciaService(options, paths, builder.Configuration, builder.Environment));

        var jwtKey = builder.Configuration["Jwt:Key"] ?? "bfs-dev-key-cambiar-en-produccion-32bytes!!";
        var signing = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey.PadRight(32, 'x')));
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

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BfsDbContext>();
            DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
            var config = scope.ServiceProvider.GetRequiredService<ConfigService>();
            app.Services.GetRequiredService<IScaleController>().Apply(config.ScaleSettings());
        }

        app.UseCors();
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            // Que el navegador (y el WebView2 de la portería) revalide siempre: al actualizar el sistema
            // no tiene que quedar corriendo el app.js de la versión anterior.
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
        });
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapAuthEndpoints();
        app.MapBrandingEndpoints();

        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/health", () => Results.Ok(new { status = "ok", version = "4.0.0-spike", modo = options.Modo.ToString() })).AllowAnonymous();
        api.MapScaleEndpoints();
        api.MapConfigEndpoints();
        api.MapCatalogoEndpoints();
        api.MapPlantaEndpoints();
        api.MapUsuarioEndpoints();
        api.MapPesadaEndpoints();
        api.MapLicenciaEndpoints();

        app.MapFallbackToFile("index.html");
        return app;
    }

    private static void AddDatosYBalanza(WebApplicationBuilder builder, BfsOptions options, BfsPaths paths)
    {
        if (options.Modo == ModoDespliegue.Nube)
        {
            var cs = builder.Configuration.GetConnectionString("Bfs");
            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new InvalidOperationException("Modo Nube requiere ConnectionStrings:Bfs (PostgreSQL).");
            }

            builder.Services.AddSingleton(NodoInfo.Ninguno);
            builder.Services.AddBfsDatabase(ModoDespliegue.Nube, cs);

            var sinBalanza = new SinBalanzaScaleReader();
            builder.Services.AddSingleton<IScaleReader>(sinBalanza);
            builder.Services.AddSingleton<IScaleController>(sinBalanza);
            return;
        }

        var respaldo = SpikeDbRespaldo.ApartarSiNoTieneMigraciones(paths.BaseSqlite);
        if (respaldo != null)
        {
            Log.Warning("La base del spike no tenía migraciones; se apartó en {Respaldo} y se crea una nueva", respaldo);
        }

        builder.Services.AddSingleton(new NodoInfo(paths.NodoId()));
        builder.Services.AddBfsDatabase(ModoDespliegue.Nodo, BfsDatabase.SqliteConnectionString(paths.BaseSqlite));

        builder.Services.AddSingleton<ConfigurableScaleReader>();
        builder.Services.AddSingleton<IScaleReader>(sp => sp.GetRequiredService<ConfigurableScaleReader>());
        builder.Services.AddSingleton<IScaleController>(sp => sp.GetRequiredService<ConfigurableScaleReader>());
    }
}
