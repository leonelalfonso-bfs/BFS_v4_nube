using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core;

namespace Bfs.Api.Config;

public static class ConfigEndpoints
{
    public static void MapConfigEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/campos", async (ConfigService config) =>
            Results.Ok(FieldCatalog.Parse(await config.ValorAsync(ConfigKeys.FieldsJson))));

        api.MapGet("/config", async (ConfigService config) =>
        {
            var items = await config.GlobalAsync();
            return Results.Ok(items.ToDictionary(
                c => c.Key,
                c => c.Key == ConfigKeys.WormsApiKey && c.Value.Length > 0 ? "********" : c.Value));
        });

        api.MapPut("/config", async (Dictionary<string, string> body, ConfigService config, IScaleController scale, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor()) return Results.Forbid();
            await config.GuardarAsync(body);
            scale.Apply(config.ScaleSettings());
            return Results.Ok(new { status = scale.Status, lastError = scale.LastError });
        });
    }
}
