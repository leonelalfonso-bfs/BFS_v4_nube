using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Api.Config;
using Bfs.Core;
using Bfs.Documents;

namespace Bfs.Api.Documentos;

public static class DocumentoEndpoints
{
    public static void MapDocumentoEndpoints(this RouteGroupBuilder api)
    {
        // Ver o bajar el PDF también es la reimpresión: el operador lo imprime desde el visor.
        api.MapGet("/pesadas/{id:guid}/ticket", async (Guid id, DocumentosService docs, ClaimsPrincipal user) =>
        {
            var datos = await docs.TicketAsync(id);
            if (datos == null || !PuedeVer(user, datos.Pesada.ClienteId)) return Results.NotFound();
            return Results.File(TicketPdf.Generar(datos), "application/pdf", $"ticket-{datos.Pesada.TicketNumero}.pdf");
        });

        api.MapPost("/pesadas/{id:guid}/termica", async (Guid id, DocumentosService docs, ConfigService config, IImpresoraTermica impresora) =>
        {
            var c = await config.GlobalAsync();
            if (c.GetValueOrDefault(ConfigKeys.Termica) != "true") return Results.BadRequest("La impresora térmica no está habilitada");
            var nombre = c.GetValueOrDefault(ConfigKeys.TermicaImpresora);
            if (string.IsNullOrWhiteSpace(nombre)) return Results.BadRequest("Falta elegir la impresora térmica en Configuración");

            var datos = await docs.TicketAsync(id);
            if (datos == null) return Results.NotFound();
            try
            {
                impresora.Imprimir(nombre, TicketTermico.Generar(datos, c.GetValueOrDefault(ConfigKeys.TermicaAncho)), $"Ticket {datos.Pesada.TicketNumero}");
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
            {
                return Results.BadRequest(ex.Message);
            }
        }).RequireAuthorization(Permisos.PesajeReimprimir);

        api.MapGet("/pesadas/excel", async (DateOnly? desde, DateOnly? hasta, Guid? empresaId, DocumentosService docs, ClaimsPrincipal user) =>
        {
            Guid? soloCliente = null;
            if (!user.Puede(Permisos.PesadasVer))
            {
                if (!user.Puede(Permisos.PesadasVerPropias) || user.ClienteId() is not { } clienteId) return Results.Forbid();
                soloCliente = clienteId;
            }
            var xlsx = await docs.ExcelAsync(desde, hasta, empresaId, soloCliente);
            return Results.File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"pesadas-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        });

        api.MapGet("/impresoras", (IImpresoraTermica impresora) => Results.Ok(impresora.Impresoras()))
            .RequireAuthorization(Permisos.ConfigBfs);
    }

    /// <summary>El usuario cliente solo ve tickets de su cliente; para él, los ajenos "no existen".</summary>
    private static bool PuedeVer(ClaimsPrincipal user, Guid? clienteDeLaPesada) =>
        user.Puede(Permisos.PesadasVer)
        || (user.Puede(Permisos.PesadasVerPropias) && user.ClienteId() is { } c && c == clienteDeLaPesada);
}
