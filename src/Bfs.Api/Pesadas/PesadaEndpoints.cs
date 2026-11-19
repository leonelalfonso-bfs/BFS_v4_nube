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

        // Congela el peso de la balanza; la entrada o salida se guarda después con el CapturaId.
        api.MapPost("/pesadas/capturar", async (CapturarRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
        {
            var (captura, error) = await pesaje.CapturarAsync(req.BalanzaId, Operador(user));
            return error != null ? ToResult(error) : Results.Ok(captura);
        }).RequireAuthorization(Permisos.PesajeOperar);

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

        api.MapPost("/pesadas/{id:guid}/corregir", async (Guid id, CorreccionRequest req, PesajeService pesaje, ClaimsPrincipal user) =>
            ToResult(await pesaje.CorregirAsync(id, req, user.Email())))
            .RequireAuthorization(Permisos.PesajeCorregir);

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
    DatosAduanaDto? Aduana = null,
    string? TipoDocumento = null,
    string? Nacionalidad = null,
    DatosInagroDto? Inagro = null,
    Guid? CapturaId = null);

/// <summary>Campos de INAGRO del formulario (edición Inagro). Los datos de cliente, transporte y planta los completa el servidor.</summary>
public record DatosInagroDto(
    int? Movimiento,
    int? PesoOrigen = null,
    string? Tanque = null,
    string? Analisis = null,
    string? Precintos = null,
    string? ProductoCodigo = null,
    string? CuilChofer = null,
    string? ObservacionesRemito = null);

/// <param name="CapturaId">Peso capturado antes con /pesadas/capturar (si no viene ni peso manual, se lee la balanza ahora).</param>
public record SalidaRequest(int? PesoSalida, int? TaraPallets, Guid? BalanzaId = null, string? ClaveManual = null, Guid? CapturaId = null);

public record CapturarRequest(Guid? BalanzaId);

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
    int? BultoTara = null,
    string? Ata = null,
    string? AtaCuit = null,
    string? DestinacionAduanera = null,
    string? ProcedenciaDestino = null,
    string? IdDestino = null);

/// <param name="Datos">Solo los campos que cambian: clave del campo (como en el formulario) → valor nuevo.</param>
public record CorreccionRequest(string? Motivo, string? Clave, Dictionary<string, string?>? Datos);
