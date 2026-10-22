using System.Security.Claims;

namespace Bfs.Api.Pesadas;

public static class PesadaEndpoints
{
    public static void MapPesadaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/pesadas", async (Guid? empresaId, PesajeService pesaje) =>
            Results.Ok(await pesaje.ListarAsync(empresaId)));

        api.MapPost("/pesadas/entrada", async (EntradaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.RegistrarEntradaAsync(req, user.FindFirstValue("username") ?? "")));

        api.MapPost("/pesadas/{id:guid}/salida", async (Guid id, SalidaRequest req, PesajeService pesaje) =>
            ToResult(await pesaje.RegistrarSalidaAsync(id, req)));
    }

    private static IResult ToResult(ResultadoPesaje r) =>
        r.NoEncontrada ? Results.NotFound()
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
    int TaraPallets);

public record SalidaRequest(int? PesoSalida, int? TaraPallets);
