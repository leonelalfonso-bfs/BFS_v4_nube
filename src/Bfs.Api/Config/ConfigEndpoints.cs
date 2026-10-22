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
                c => c.Key is ConfigKeys.WormsApiKey or ConfigKeys.ClaveManualHash && c.Value.Length > 0 ? "********" : c.Value));
        }).RequireAuthorization(Permisos.ConfigBfs);

        api.MapPut("/config", async (Dictionary<string, string> body, ConfigService config) =>
        {
            await config.GuardarAsync(body);
            return Results.NoContent();
        }).RequireAuthorization(Permisos.ConfigBfs);
    }
}
