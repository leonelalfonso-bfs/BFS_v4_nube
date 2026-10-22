using Bfs.Api.Config;
using Bfs.Core;

namespace Bfs.Api.Balanza;

public static class ScaleEndpoints
{
    public static void MapScaleEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/scale/peso", (IScaleReader scale) => Results.Ok(scale.Current))
            .RequireAuthorization(Permisos.PesajeOperar);

        api.MapGet("/scale/estado", (IScaleController scale) => Results.Ok(new
        {
            status = scale.Status,
            lastError = scale.LastError,
            settings = scale.Settings,
            puertos = scale.ListPorts()
        })).RequireAuthorization(Permisos.ConfigBfs);

        api.MapGet("/scale/puertos", (IScaleController scale) => Results.Ok(scale.ListPorts()))
            .RequireAuthorization(Permisos.ConfigBfs);

        api.MapPost("/scale/aplicar", (ConfigService config, IScaleController scale) =>
        {
            scale.Apply(config.ScaleSettings());
            return Results.Ok(new { status = scale.Status, lastError = scale.LastError, settings = scale.Settings });
        }).RequireAuthorization(Permisos.ConfigBfs);
    }
}
