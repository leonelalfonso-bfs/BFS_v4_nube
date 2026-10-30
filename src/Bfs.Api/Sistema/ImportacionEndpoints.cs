using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Importacion;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Sistema;

/// <summary>Importación de una base de la 3.x. La hace BFS al instalar; antes se guarda un backup de la base 4.0.</summary>
public static class ImportacionEndpoints
{
    public static string RutaPorDefecto3x()
    {
        var programData = Environment.GetEnvironmentVariable("ProgramData") ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "bfs-truckscale-data", "truckscale.db");
    }

    public static void MapImportacionEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sistema/importar-3x").RequireAuthorization(Permisos.ConfigBfs);

        g.MapGet("", (string? ruta) =>
        {
            var archivo = string.IsNullOrWhiteSpace(ruta) ? RutaPorDefecto3x() : ruta;
            if (!File.Exists(archivo)) return Results.Ok(new { ruta = archivo, existe = false });
            try
            {
                return Results.Ok(new { ruta = archivo, existe = true, contenido = Importador3x.Contar(archivo) });
            }
            catch (SqliteException ex)
            {
                return Results.BadRequest($"No se pudo leer {archivo}: {ex.Message}");
            }
        });

        g.MapPost("", async (HttpRequest request, BfsDbContext db, PasswordHasher<Usuario> hasher, BfsOptions options, BfsPaths paths,
            SistemaService sistema, IBalanzas balanzas, Nube.TenantActual tenant, ILogger<Importador3x> log) =>
        {
            string archivo;
            string? temporal = null;
            if (request.HasFormContentType)
            {
                if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
                {
                    limite.MaxRequestBodySize = SistemaEndpoints.MaxRestaurar;
                }
                var form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = SistemaEndpoints.MaxRestaurar });
                if (form.Files.FirstOrDefault() is { Length: > 0 } subido)
                {
                    temporal = Path.Combine(Path.GetTempPath(), $"truckscale-{Guid.NewGuid():N}.db");
                    await using (var f = File.Create(temporal)) await subido.CopyToAsync(f);
                    archivo = temporal;
                }
                else
                {
                    archivo = string.IsNullOrWhiteSpace(form["ruta"]) ? RutaPorDefecto3x() : form["ruta"].ToString();
                }
            }
            else
            {
                archivo = RutaPorDefecto3x();
            }

            try
            {
                if (!File.Exists(archivo)) return Results.BadRequest($"No se encontró {archivo}");
                // Siempre la misma: el nombre no sirve para ordenar porque la importación lo cambia.
                var empresa = await db.Empresas.OrderBy(e => e.CreadoEn).ThenBy(e => e.Id).FirstAsync();
                Backup? backup = options.Modo == ModoDespliegue.Nodo ? sistema.CrearBackup() : null;

                var resultado = await new Importador3x(db, hasher, options.ZonaHoraria, tenant.CarpetaBranding(paths)).ImportarAsync(archivo, empresa.Id);
                log.LogInformation("Importación 3.x desde {Archivo}: {@Conteos}", archivo, resultado.Conteos);
                await Balanzas.BalanzaEndpoints.AplicarAsync(db, balanzas);
                return Results.Ok(new { resultado.Conteos, resultado.Avisos, backupPrevio = backup?.Archivo });
            }
            catch (Exception ex) when (ex is InvalidOperationException or SqliteException or IOException)
            {
                return Results.BadRequest("No se pudo importar: " + ex.Message);
            }
            finally
            {
                if (temporal != null)
                {
                    SqliteConnection.ClearAllPools();
                    try { File.Delete(temporal); } catch (IOException) { }
                }
            }
        }).DisableAntiforgery();
    }
}
