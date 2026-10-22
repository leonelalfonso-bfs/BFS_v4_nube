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

        api.MapGet("/pesadas/pendientes", async (PesajeService pesaje) => Results.Ok(await pesaje.PendientesAsync()))
            .RequireAuthorization(Permisos.PesajeOperar);

        api.MapPost("/pesadas/entrada", async (EntradaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.RegistrarEntradaAsync(req, Operador(user))))
            .RequireAuthorization(Permisos.PesajeOperar);

        api.MapPost("/pesadas/{id:guid}/salida", async (Guid id, SalidaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.RegistrarSalidaAsync(id, req, Operador(user))))
            .RequireAuthorization(Permisos.PesajeOperar);

        api.MapPost("/pesadas/tara-conocida", async (TaraConocidaRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.RegistrarConTaraConocidaAsync(req, Operador(user))))
            .RequireAuthorization(Permisos.PesajeOperar);

        api.MapPost("/pesadas/{id:guid}/anular", async (Guid id, AnularRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.AnularAsync(id, req.Motivo, Operador(user))))
            .RequireAuthorization(Permisos.PesajeAnular);

        api.MapPost("/taras/tomar", async (TomarTaraRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
        {
            var (tara, error) = await pesaje.TomarTaraAsync(req, Operador(user));
            return error != null ? ToResult(error) : Results.Ok(tara);
        }).RequireAuthorization(Permisos.PesajeOperar);
    }

    private static Operador Operador(ClaimsPrincipal user) => new(user.Email(), user.Puede(Permisos.PesajePesoManual));

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
    Guid? BalanzaId = null,
    string? ClaveManual = null,
    string? TipoOperacion = null,
    DatosAduanaDto? Aduana = null);

public record SalidaRequest(int? PesoSalida, int? TaraPallets, Guid? BalanzaId = null, string? ClaveManual = null);

/// <param name="Datos">Los mismos datos que una entrada; PesoEntrada es el bruto si se carga a mano.</param>
/// <param name="TaraManual">Solo si se quiere usar otra tara que la guardada (cuenta como ingreso manual).</param>
public record TaraConocidaRequest(EntradaRequest Datos, int? TaraManual = null, string? ClaveManual = null);

public record TomarTaraRequest(string? Identificador, string? Descripcion, Guid? BalanzaId = null, int? PesoManual = null, string? ClaveManual = null);

public record AnularRequest(string? Motivo);

public record DatosAduanaDto(
    string Escenario,
    string? Exportador = null,
    string? ExportadorCuit = null,
    string? OperacionId = null,
    string? ChoferTipoDocumento = null,
    string? ChoferNacionalidad = null,
    string? ContenedorId = null,
    int ContenedorTara = 0,
    string? BultoId = null,
    int? BultoBruto = null,
    int? BultoTara = null);
