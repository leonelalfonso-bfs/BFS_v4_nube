using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Api.Config;
using Bfs.Core;

namespace Bfs.Api.Balanza;

public static class ScaleEndpoints
{
    public static void MapScaleEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/scale/peso", (IScaleReader scale) => Results.Ok(scale.Current));

        api.MapGet("/scale/estado", (IScaleController scale) => Results.Ok(new
        {
            status = scale.Status,
            lastError = scale.LastError,
            settings = scale.Settings,
            puertos = scale.ListPorts()
        }));

        api.MapGet("/scale/puertos", (IScaleController scale) => Results.Ok(scale.ListPorts()));

        api.MapPost("/scale/aplicar", (ConfigService config, IScaleController scale, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor()) return Results.Forbid();
            scale.Apply(config.ScaleSettings());
            return Results.Ok(new { status = scale.Status, lastError = scale.LastError, settings = scale.Settings });
        });
    }
}
