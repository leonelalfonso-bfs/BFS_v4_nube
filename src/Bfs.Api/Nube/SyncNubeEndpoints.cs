using System.Security.Cryptography;
using System.Text.Json;
using Bfs.Api.Auth;
using Bfs.Core.Entities;
using Bfs.Core.Licencias;
using Bfs.Infrastructure.Central;
using Bfs.Infrastructure.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Nube;

/// <summary>Lado nube de la sincronización: alta de porterías (nodos), activación y el intercambio de cambios.</summary>
public static class SyncNubeEndpoints
{
    public static readonly TimeSpan VigenciaCodigo = TimeSpan.FromDays(7);

    public static void MapSyncNubeEndpoints(this WebApplication app)
    {
        var sa = app.MapGroup("/api/superadmin/tenants/{id:guid}/nodos").RequireAuthorization(Autorizacion.PoliticaSuperadmin);

        sa.MapGet("", async (Guid id, BasesNube bases) =>
        {
            await using var central = bases.Central();
            var nodos = await central.Nodos.AsNoTracking().Where(n => n.TenantId == id).OrderBy(n => n.Nombre).ToListAsync();
            return Results.Ok(nodos.Select(n => new { n.Id, n.Nombre, n.Maquina, n.CreadoEn, n.ActivadoEn, n.UltimaSincronizacion, codigoPendiente = n.CodigoActivacionHash != null && n.CodigoVence > DateTimeOffset.UtcNow }));
        });

        // Crea la portería (o genera un código nuevo si se reinstaló la PC). El código se muestra una sola vez.
        sa.MapPost("", async (Guid id, NuevoNodo dto, BasesNube bases) =>
        {
            await using var central = bases.Central();
            if (!await central.Tenants.AnyAsync(t => t.Id == id)) return Results.NotFound();
            var nombre = string.IsNullOrWhiteSpace(dto.Nombre) ? "Portería" : dto.Nombre.Trim();
            var nodo = dto.NodoId is { } existente
                ? await central.Nodos.FirstOrDefaultAsync(n => n.Id == existente && n.TenantId == id)
                : null;
            if (dto.NodoId != null && nodo == null) return Results.NotFound();
            if (nodo == null)
            {
                nodo = new Nodo { Id = Guid.NewGuid(), TenantId = id, Nombre = nombre, CreadoEn = DateTimeOffset.UtcNow };
                central.Nodos.Add(nodo);
            }
            var codigo = SecretosNodo.NuevoCodigo();
            nodo.CodigoActivacionHash = SecretosNodo.HashCodigo(codigo);
            nodo.CodigoVence = DateTimeOffset.UtcNow.Add(VigenciaCodigo);
            nodo.TokenHash = null;   // un código nuevo invalida la conexión anterior
            await central.SaveChangesAsync();
            return Results.Ok(new { nodoId = nodo.Id, nodo.Nombre, codigo, vence = nodo.CodigoVence });
        });

        app.MapPost("/api/sync/activar", async (PedidoActivacion req, BasesNube bases) =>
        {
            if (string.IsNullOrWhiteSpace(req.Codigo)) return Results.BadRequest("Falta el código");
            await using var central = bases.Central();
            var ahora = DateTimeOffset.UtcNow;
            var hash = SecretosNodo.HashCodigo(req.Codigo);
            var nodo = await central.Nodos.FirstOrDefaultAsync(n => n.CodigoActivacionHash == hash && n.CodigoVence > ahora);
            if (nodo == null) return Results.BadRequest("Código inválido o vencido. Pedí uno nuevo a BFS.");
            var tenant = await central.Tenants.AsNoTracking().FirstAsync(t => t.Id == nodo.TenantId);
            if (tenant.Estado != EstadosTenant.Activo) return EstadoTenantMiddleware.Bloqueado(tenant);

            var token = SecretosNodo.NuevoToken();
            nodo.TokenHash = SecretosNodo.HashToken(token);
            nodo.CodigoActivacionHash = null;
            nodo.CodigoVence = null;
            nodo.ActivadoEn = ahora;
            nodo.Maquina = req.Maquina;
            await central.SaveChangesAsync();
            return Results.Ok(new RespuestaActivacion(nodo.Id, token, tenant.Nombre));
        });

        app.MapPost("/api/sync", Sincronizar);
    }

    /// <summary>Autenticación de la portería: <c>Authorization: Nodo {nodoId}:{token}</c>.</summary>
    private static async Task<IResult> Sincronizar(HttpRequest request, PedidoSync pedido, BasesNube bases, OpcionesNube opciones, ILogger<PedidoSync> log)
    {
        var auth = request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Nodo ", StringComparison.Ordinal)) return Results.Unauthorized();
        var partes = auth[5..].Split(':', 2);
        if (partes.Length != 2 || !Guid.TryParse(partes[0], out var nodoId)) return Results.Unauthorized();

        await using var central = bases.Central();
        var nodo = await central.Nodos.FirstOrDefaultAsync(n => n.Id == nodoId);
        if (nodo == null || !SecretosNodo.TokenValido(partes[1], nodo.TokenHash)) return Results.Unauthorized();
        var tenant = await central.Tenants.AsNoTracking().FirstAsync(t => t.Id == nodo.TenantId);
        // Suspendido: no recibe ni manda nada, y no se le renueva la licencia (al vencer, la portería deja de pesar).
        if (tenant.Estado != EstadosTenant.Activo) return EstadoTenantMiddleware.Bloqueado(tenant);

        await using var db = bases.Tenant(tenant.Codigo);
        var aplicados = new Dictionary<string, ResultadoAplicar>();
        foreach (var tipo in TiposSync.Subida)
        {
            if (pedido.Cambios.TryGetValue(tipo, out var filas) && filas.Count > 0)
            {
                aplicados[tipo] = await AplicadorSync.AplicarAsync(db, tipo, filas, esNube: true);
            }
        }

        var cambios = new Dictionary<string, List<JsonElement>>();
        var hasta = new Dictionary<string, long>();
        foreach (var tipo in TiposSync.Bajada)
        {
            var desde = pedido.Desde.GetValueOrDefault(tipo);
            var minimo = Math.Max(0, desde - SecretosNodo.Solapamiento.Ticks);
            var filas = await TiposSync.Consulta(db, tipo).AsNoTracking()
                .Where(x => x.SyncSeq > minimo).OrderBy(x => x.SyncSeq).Take(SecretosNodo.Lote).ToListAsync();
            cambios[tipo] = filas.Select(f => JsonSerializer.SerializeToElement(f, TiposSync.Clr(tipo), AplicadorSync.Json)).ToList();
            hasta[tipo] = filas.Count > 0 ? Math.Max(desde, filas[^1].SyncSeq) : desde;
        }

        nodo.UltimaSincronizacion = DateTimeOffset.UtcNow;
        nodo.Maquina = pedido.Maquina;
        await central.SaveChangesAsync();

        var conflictos = aplicados.Values.Sum(a => a.Conflictos.Count);
        if (conflictos > 0) log.LogWarning("Sync {Tenant}/{Nodo}: {Conflictos} conflictos", tenant.Codigo, nodo.Nombre, conflictos);
        return Results.Ok(new RespuestaSync(tenant.Nombre, aplicados, cambios, hasta, Licencia(opciones, tenant, pedido.Maquina)));
    }

    /// <summary>Licencia de la portería por <see cref="OpcionesNube.DiasLicencia"/> días: si deja de sincronizar o se suspende, vence sola.</summary>
    private static string? Licencia(OpcionesNube opciones, Tenant tenant, string maquina)
    {
        var pem = opciones.ClaveLicencias;
        if (string.IsNullOrWhiteSpace(pem)) return null;
        if (!pem.TrimStart().StartsWith("-----BEGIN", StringComparison.Ordinal)) pem = File.ReadAllText(pem);
        using var clave = ECDsa.Create();
        clave.ImportFromPem(pem);
        var ahora = DateTimeOffset.UtcNow;
        return LicenciaCodec.Firmar(new Licencia(tenant.Nombre, maquina, ahora, ahora.AddDays(opciones.DiasLicencia)), clave);
    }
}

/// <param name="NodoId">Para una portería que ya existía (reinstalación): genera otro código.</param>
public record NuevoNodo(string? Nombre, Guid? NodoId = null);
