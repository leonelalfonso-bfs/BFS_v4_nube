using Bfs.Api.Config;
using Bfs.Core;
using Microsoft.AspNetCore.Http.Features;

namespace Bfs.Api.Sistema;

public static class SistemaEndpoints
{
    public const long MaxRestaurar = 2L * 1024 * 1024 * 1024;

    public static void MapSistemaEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sistema").RequireAuthorization(Permisos.SistemaGestionar);

        g.MapGet("", async (SistemaService sistema, ConfigService config, BfsOptions options) =>
        {
            if (options.Modo == ModoDespliegue.Nube) return SoloNodo();
            var c = await config.GlobalAsync();
            return Results.Ok(new
            {
                backups = sistema.Backups(),
                restauracionPendiente = sistema.RestauracionPendiente,
                espejoCarpeta = c.GetValueOrDefault(ConfigKeys.EspejoCarpeta, ""),
                espejoUltimo = c.GetValueOrDefault(ConfigKeys.EspejoUltimo, ""),
                espejoError = c.GetValueOrDefault(ConfigKeys.EspejoError, ""),
                tickets = await sistema.NumeracionAsync()
            });
        });

        g.MapPost("/backups", (SistemaService sistema, BfsOptions options) =>
            options.Modo == ModoDespliegue.Nube ? SoloNodo() : Results.Ok(sistema.CrearBackup()));

        g.MapGet("/backups/{archivo}", (string archivo, SistemaService sistema) =>
            sistema.RutaBackup(archivo) is { } ruta
                ? Results.File(ruta, "application/octet-stream", Path.GetFileName(ruta))
                : Results.NotFound());

        g.MapPost("/restaurar", async (HttpRequest request, SistemaService sistema, BfsOptions options) =>
        {
            if (options.Modo == ModoDespliegue.Nube) return SoloNodo();
            if (!request.HasFormContentType) return Results.BadRequest("Elegí el archivo de backup");
            // Un backup puede pesar mucho más que los 30 MB que Kestrel deja por defecto.
            if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limite)
            {
                limite.MaxRequestBodySize = MaxRestaurar;
            }
            var form = await request.ReadFormAsync(new FormOptions { MultipartBodyLengthLimit = MaxRestaurar });
            var file = form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return Results.BadRequest("Elegí el archivo de backup");
            if (file.Length > MaxRestaurar) return Results.BadRequest("El archivo es demasiado grande");

            await using var stream = file.OpenReadStream();
            var error = await sistema.PrepararRestauracionAsync(stream);
            return error == null
                ? Results.Ok(new { mensaje = "Listo. Cerrá y volvé a abrir el sistema para terminar la restauración (la base actual queda guardada en backups)." })
                : Results.BadRequest(error);
        }).DisableAntiforgery();

        g.MapDelete("/restaurar", (SistemaService sistema) =>
        {
            sistema.CancelarRestauracion();
            return Results.NoContent();
        });

        g.MapPut("/espejo", async (EspejoDto dto, ConfigService config) =>
        {
            var carpeta = dto.Carpeta?.Trim() ?? "";
            if (carpeta.Length > 0 && !Path.IsPathFullyQualified(carpeta)) return Results.BadRequest("Indicá una ruta completa, por ejemplo D:\\Espejo o \\\\servidor\\carpeta");
            await config.GuardarAsync(new Dictionary<string, string> { [ConfigKeys.EspejoCarpeta] = carpeta, [ConfigKeys.EspejoError] = "" });
            return Results.NoContent();
        });

        g.MapPost("/espejo", async (SistemaService sistema) =>
            await sistema.ActualizarEspejoAsync() is { } error ? Results.BadRequest(error) : Results.NoContent());

        g.MapPut("/tickets", async (ProximoTicketDto dto, SistemaService sistema) =>
            await sistema.FijarProximoAsync(dto.EmpresaId, dto.Proximo) is { } error ? Results.BadRequest(error) : Results.NoContent());
    }

    private static IResult SoloNodo() => Results.BadRequest("En la nube los backups los hace BFS en el servidor");
}

public record EspejoDto(string? Carpeta);
public record ProximoTicketDto(Guid EmpresaId, int Proximo);
