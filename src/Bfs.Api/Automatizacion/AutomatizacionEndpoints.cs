using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Api.Config;
using Bfs.Core;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Automatizacion;

public record CambioSalida(bool Valor);

public static class AutomatizacionEndpoints
{
    private const string Oculta = "********";

    public static void MapAutomatizacionEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/automatizacion");

        // Lo que muestra Pesaje: sensores, semáforos, barreras y cámaras. Apagada devuelve todo en falso, sin tocar la placa.
        g.MapGet("/estado", (AutomatizacionService a) => Results.Ok(a.Estado()))
            .RequireAuthorization(Permisos.PesajeOperar);

        // El operador cambia un semáforo o abre / cierra una barrera.
        g.MapPost("/salidas/{punto:int}", async (int punto, CambioSalida req, AutomatizacionService a) =>
            await a.CambiarSalidaAsync(punto, req.Valor) is { } error ? Results.BadRequest(error) : Results.Ok(a.Estado()))
            .RequireAuthorization(Permisos.PesajeOperar);

        // Vista en vivo: la foto actual de la cámara (la pantalla la vuelve a pedir varias veces por segundo).
        g.MapGet("/camaras/{indice:int}", async (int indice, HttpContext http, CamarasService camaras) =>
        {
            var foto = await camaras.FotoAsync(indice);
            if (foto == null) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(foto.Datos, foto.Tipo);
        }).RequireAuthorization(Permisos.PesajeOperar);

        g.MapGet("/config", (AutomatizacionService a) =>
        {
            var c = ConfigAutomatizacion.Parse(a.Config.ToJson());
            c.Camaras = c.Camaras.Select(x => x with { Clave = string.IsNullOrEmpty(x.Clave) ? x.Clave : Oculta }).ToList();
            return Results.Ok(c);
        }).RequireAuthorization(Permisos.ConfigBfs);

        g.MapPut("/config", async (ConfigAutomatizacion nueva, AutomatizacionService a, ConfigService config) =>
        {
            nueva.Dispositivos ??= ConfigAutomatizacion.Predeterminados();
            nueva.Camaras ??= [];
            if (nueva.Validar() is { } error) return Results.BadRequest(error);
            // Las claves de las cámaras vienen ocultas: si no las cambiaron, quedan las que estaban.
            nueva.Camaras = nueva.Camaras.Select(c => c.Clave == Oculta
                ? c with { Clave = a.Config.Camaras.FirstOrDefault(v => v.Url == c.Url && v.Usuario == c.Usuario)?.Clave }
                : c).ToList();
            await config.GuardarAsync(new Dictionary<string, string> { [ConfigKeys.AutomatizacionJson] = nueva.ToJson() });
            await a.RecargarAsync();
            return Results.Ok(a.Estado());
        }).RequireAuthorization(Permisos.ConfigBfs);

        // Pantalla de prueba con la placa simulada: cortar o liberar un sensor.
        g.MapPost("/simular/{punto:int}", (int punto, CambioSalida req, AutomatizacionService a) =>
            a.SimularEntrada(punto, req.Valor) ? Results.Ok(a.Estado()) : Results.BadRequest("Solo con la placa simulada"))
            .RequireAuthorization(Permisos.ConfigBfs);

        // Probar una cámara antes de guardar.
        g.MapPost("/camaras/probar", async (CamaraAutomatizacion camara, AutomatizacionService a, CamarasService camaras) =>
        {
            if (camara.Clave == Oculta)
            {
                camara = camara with { Clave = a.Config.Camaras.FirstOrDefault(v => v.Url == camara.Url && v.Usuario == camara.Usuario)?.Clave };
            }
            var foto = await camaras.ProbarAsync(camara);
            return foto == null
                ? Results.BadRequest("La cámara no respondió (revisá la dirección, el usuario y la clave)")
                : Results.File(foto.Datos, foto.Tipo);
        }).RequireAuthorization(Permisos.ConfigBfs);

        // Fotos guardadas con una pesada (en el puesto que pesó).
        api.MapGet("/pesadas/{id:guid}/fotos/{n:int}", async (Guid id, int n, BfsDbContext db, FotosPesadas fotos, ClaimsPrincipal user) =>
        {
            var p = await db.Pesadas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return Results.NotFound();
            var puede = user.Puede(Permisos.PesadasVer) || (user.Puede(Permisos.PesadasVerPropias) && user.ClienteId() is { } c && c == p.ClienteId);
            if (!puede) return Results.NotFound();
            var lista = AutomatizacionPesaje.Leer(p);
            if (n < 0 || n >= lista.Count || fotos.Leer(lista[n]) is not { } datos) return Results.NotFound();
            return Results.File(datos, CamarasService.TipoImagen(datos));
        });
    }
}
