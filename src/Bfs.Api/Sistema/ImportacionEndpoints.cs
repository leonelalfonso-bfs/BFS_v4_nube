using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Infrastructure.Importacion;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Sistema;

/// <summary>Importación de una base anterior (1.x <c>.sql</c> o 3.x <c>truckscale.db</c>). Antes se guarda un backup de la base 4.0.</summary>
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
                var es1x = Importador1x.EsBase1x(archivo);
                var contenido = es1x ? Importador1x.Contar(archivo) : Importador3x.Contar(archivo);
                return Results.Ok(new { ruta = archivo, existe = true, version = es1x ? "1.x" : "3.x", contenido });
            }
            catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
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
                    var ext = string.Equals(Path.GetExtension(subido.FileName), ".sql", StringComparison.OrdinalIgnoreCase) ? ".sql" : ".db";
                    temporal = Path.Combine(Path.GetTempPath(), $"bfs-import-{Guid.NewGuid():N}{ext}");
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

                var es1x = Importador1x.EsBase1x(archivo);
                var carpetaBranding = tenant.CarpetaBranding(paths);
                var resultado = es1x
                    ? await new Importador1x(db, hasher, options.ZonaHoraria, carpetaBranding).ImportarAsync(archivo, empresa.Id)
                    : await new Importador3x(db, hasher, options.ZonaHoraria, carpetaBranding).ImportarAsync(archivo, empresa.Id);
                log.LogInformation("Importación {Version} desde {Archivo}: {@Conteos}", es1x ? "1.x" : "3.x", archivo, resultado.Conteos);
                await Balanzas.BalanzaEndpoints.AplicarAsync(db, balanzas);
                return Results.Ok(new { resultado.Conteos, resultado.Avisos, backupPrevio = backup?.Archivo, version = es1x ? "1.x" : "3.x" });
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
