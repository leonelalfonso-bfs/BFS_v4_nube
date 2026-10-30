using Bfs.Core;

namespace Bfs.Api.Sincronizacion;

/// <summary>Configuración → Nube en la portería: conectarla con la nube de BFS y ver cómo va la sincronización.</summary>
public static class NubeNodoEndpoints
{
    public static void MapNubeNodoEndpoints(this RouteGroupBuilder api)
    {
        // El estado lo ve cualquiera (va en la barra de arriba: "En línea" / "Sin conexión, N pendientes").
        api.MapGet("/nube", async (SincronizadorNodo sync) => Results.Ok(await sync.EstadoAsync()));

        var g = api.MapGroup("/nube").RequireAuthorization(Permisos.ConfigBfs);

        g.MapPost("/activar", async (ActivarNube dto, SincronizadorNodo sync) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Url) || string.IsNullOrWhiteSpace(dto.Codigo)) return Results.BadRequest("Falta la dirección o el código");
            return await sync.ActivarAsync(dto.Url, dto.Codigo) is { } error
                ? Results.BadRequest(error)
                : Results.Ok(await sync.EstadoAsync());
        });

        g.MapPost("/sincronizar", async (SincronizadorNodo sync) =>
            await sync.SincronizarAsync() is { } error ? Results.BadRequest(error) : Results.Ok(await sync.EstadoAsync()));

        g.MapDelete("", (SincronizadorNodo sync) =>
        {
            sync.Desvincular();
            return Results.NoContent();
        });
    }
}

public record ActivarNube(string? Url, string? Codigo);
