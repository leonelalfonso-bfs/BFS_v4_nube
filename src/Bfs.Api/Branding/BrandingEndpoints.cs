using Bfs.Api.Config;
using Bfs.Core;

namespace Bfs.Api.Branding;

public static class BrandingEndpoints
{
    public static void MapBrandingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branding", async (ConfigService config) =>
        {
            var c = await config.GlobalAsync();
            var logo = c.GetValueOrDefault(ConfigKeys.LogoFile, "");
            return Results.Ok(new
            {
                clienteNombre = c.GetValueOrDefault(ConfigKeys.ClienteNombre, ""),
                ticketTitulo = c.GetValueOrDefault(ConfigKeys.TicketTitulo, "Comprobante de pesaje"),
                ticketPie = c.GetValueOrDefault(ConfigKeys.TicketPie, ""),
                logoUrl = string.IsNullOrWhiteSpace(logo) ? null : "/api/branding/logo"
            });
        });

        app.MapGet("/api/branding/logo", async (ConfigService config, BfsPaths paths) =>
        {
            var logo = await config.ValorAsync(ConfigKeys.LogoFile);
            if (string.IsNullOrWhiteSpace(logo)) return Results.NotFound();
            var path = Path.Combine(paths.Branding, Path.GetFileName(logo));
            if (!File.Exists(path)) return Results.NotFound();
            var content = Path.GetExtension(logo).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
            return Results.File(path, content);
        });

        app.MapPost("/api/branding/logo", async (HttpRequest request, ConfigService config, BfsPaths paths) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest("Archivo requerido");
            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return Results.BadRequest("Archivo requerido");
            if (file.Length > 2 * 1024 * 1024) return Results.BadRequest("El logo no puede superar 2 MB");
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".svg"))
            {
                return Results.BadRequest("Usá PNG, JPG, WEBP o SVG");
            }

            var dir = paths.Branding;
            foreach (var old in Directory.GetFiles(dir, "logo.*"))
            {
                try { File.Delete(old); } catch { /* ignore locked files */ }
            }

            var name = "logo" + ext;
            await using (var stream = File.Create(Path.Combine(dir, name)))
            {
                await file.CopyToAsync(stream);
            }

            await config.GuardarAsync([new(ConfigKeys.LogoFile, name)]);
            return Results.Ok(new { logoUrl = "/api/branding/logo" });
        }).RequireAuthorization(Permisos.ConfigBfs).DisableAntiforgery();
    }
}
