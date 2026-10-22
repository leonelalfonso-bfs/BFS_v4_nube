using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core;

namespace Bfs.Api.Pesadas;

public static class PesadaEndpoints
{
    public static void MapPesadaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/pesadas", async (Guid? empresaId, PesajeService pesaje, ClaimsPrincipal user) =>
        {
            if (user.Puede(Permisos.PesadasVer))
            {
                return Results.Ok(await pesaje.ListarAsync(empresaId));
            }
            if (user.Puede(Permisos.PesadasVerPropias) && user.ClienteId() is { } clienteId)
            {
                return Results.Ok(await pesaje.ListarAsync(empresaId, clienteId));
            }
            return Results.Forbid();
        });

        api.MapPost("/pesadas/entrada", async (EntradaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
        {
            if (req.PesoEntrada.HasValue && !user.Puede(Permisos.PesajePesoManual)) return SinPermisoPesoManual();
            return ToResult(await pesaje.RegistrarEntradaAsync(req, user.Email()));
        }).RequireAuthorization(Permisos.PesajeOperar);

        api.MapPost("/pesadas/{id:guid}/salida", async (Guid id, SalidaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
        {
            if (req.PesoSalida.HasValue && !user.Puede(Permisos.PesajePesoManual)) return SinPermisoPesoManual();
            return ToResult(await pesaje.RegistrarSalidaAsync(id, req));
        }).RequireAuthorization(Permisos.PesajeOperar);
    }

    private static IResult SinPermisoPesoManual() =>
        Results.Text("No tenés permiso para cargar el peso a mano", statusCode: StatusCodes.Status403Forbidden);

    private static IResult ToResult(ResultadoPesaje r) =>
        r.NoEncontrada ? Results.NotFound()
        : r.Bloqueada ? Results.Text(r.Error, statusCode: StatusCodes.Status403Forbidden)
        : r.Error != null ? Results.BadRequest(r.Error)
        : Results.Ok(r.Pesada);
}

public record EntradaRequest(
    Guid EmpresaId,
    string PatenteChasis,
    string? PatenteAcoplado,
    string? Conductor,
    string? Documento,
    string? Cliente,
    string? Transporte,
    string? Producto,
    string? Procedencia,
    string? Destino,
    string? Comprobante,
    string? Observaciones,
    string? Tipo,
    int? PesoEntrada,
    int TaraPallets,
    Guid? ClienteId = null,
    Guid? BalanzaId = null);

public record SalidaRequest(int? PesoSalida, int? TaraPallets, Guid? BalanzaId = null);
