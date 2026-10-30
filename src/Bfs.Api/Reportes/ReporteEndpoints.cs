using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core;

namespace Bfs.Api.Reportes;

public static class ReporteEndpoints
{
    public static void MapReporteEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/reportes/pesadas", async ([AsParameters] FiltroQuery q, ReportesService reportes, ClaimsPrincipal user) =>
        {
            var filtro = Filtro(q, user);
            return filtro == null ? Results.Forbid() : Results.Ok(await reportes.ListarAsync(filtro, q.Pagina ?? 1, q.Tamano ?? 50));
        });

        api.MapGet("/reportes/resumen", async (int? dias, ReportesService reportes, ClaimsPrincipal user) =>
        {
            var filtro = Filtro(new FiltroQuery(), user);
            return filtro == null ? Results.Forbid() : Results.Ok(await reportes.ResumenAsync(dias ?? 30, filtro.SoloCliente));
        });
    }

    /// <summary>
    /// Arma el filtro y le agrega la restricción del usuario: el rol cliente siempre ve solo lo suyo,
    /// diga lo que diga la consulta. Null = no puede ver pesadas.
    /// </summary>
    public static FiltroPesadas? Filtro(FiltroQuery q, ClaimsPrincipal user)
    {
        Guid? soloCliente = null;
        if (!user.Puede(Permisos.PesadasVer))
        {
            if (!user.Puede(Permisos.PesadasVerPropias) || user.ClienteId() is not { } clienteId) return null;
            soloCliente = clienteId;
        }
        return new FiltroPesadas(q.Desde, q.Hasta, q.EmpresaId, q.Cliente, q.Transporte, q.Producto, q.Texto, q.Estado, soloCliente);
    }
}

/// <summary>Parámetros de la URL: ?desde=2026-10-01&amp;hasta=2026-10-31&amp;cliente=agro&amp;estado=SALIDA&amp;pagina=2</summary>
public record FiltroQuery(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    Guid? EmpresaId = null,
    string? Cliente = null,
    string? Transporte = null,
    string? Producto = null,
    string? Texto = null,
    string? Estado = null,
    int? Pagina = null,
    int? Tamano = null);
