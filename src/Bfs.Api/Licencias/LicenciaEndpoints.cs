using Bfs.Core;

namespace Bfs.Api.Licencias;

public static class LicenciaEndpoints
{
    public static void MapLicenciaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/licencia", (LicenciaService licencias) => Results.Ok(Respuesta(licencias.Estado())));

        api.MapPost("/licencia", (ActivarLicencia req, LicenciaService licencias) =>
        {
            var info = licencias.Activar(req.Texto ?? "");
            return info.Estado == EstadoLicencia.Valida
                ? Results.Ok(Respuesta(info))
                : Results.BadRequest(info.Mensaje);
        }).RequireAuthorization(Permisos.LicenciaGestionar);
    }

    private static object Respuesta(InfoLicencia i) => new
    {
        estado = i.Estado.ToString(),
        i.Mensaje,
        i.PuedePesar,
        i.Maquina,
        i.Maquinas,
        cliente = i.Licencia?.Cliente,
        vence = i.Licencia?.Vence
    };
}

public record ActivarLicencia(string? Texto);
