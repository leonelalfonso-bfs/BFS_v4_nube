using System.Linq.Expressions;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Catalogos;

/// <summary>
/// Catálogos de la planta. Todos tienen el mismo contrato:
/// <c>GET /catalogos/{tipo}?q=texto</c>, <c>POST</c>, <c>PUT /{id}</c> y <c>DELETE /{id}</c> (borrado lógico).
/// El nombre no se repite (sin distinguir mayúsculas); dar de alta uno borrado lo recupera.
/// </summary>
public static class CatalogoEndpoints
{
    public const int MaxResultados = 500;

    public static void MapCatalogoEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/catalogos");

        Map<Cliente, ClienteDto>(g, "clientes", db => db.Clientes, c => c.RazonSocial,
            dto => dto.RazonSocial,
            (dto, e) =>
            {
                var (cuit, error) = Cuit.Normalizar(dto.Cuit);
                if (error != null) return error;
                e.RazonSocial = dto.RazonSocial!.Trim();
                e.Cuit = cuit;
                return null;
            },
            async (db, e) => await db.Usuarios.AnyAsync(u => u.ClienteId == e.Id)
                ? "Tiene usuarios de portal asociados: dalos de baja o cambiales el cliente primero"
                : null);

        Map<Transporte, TransporteDto>(g, "transportes", db => db.Transportes, t => t.RazonSocial,
            dto => dto.RazonSocial,
            (dto, e) =>
            {
                var (cuit, error) = Cuit.Normalizar(dto.Cuit);
                if (error != null) return error;
                if (dto.EsAta && string.IsNullOrWhiteSpace(dto.CodigoAta)) return "Un ATA necesita código";
                e.RazonSocial = dto.RazonSocial!.Trim();
                e.Cuit = cuit;
                e.EsAta = dto.EsAta;
                e.CodigoAta = dto.EsAta ? dto.CodigoAta!.Trim() : null;
                return null;
            });

        Map<Producto, ProductoDto>(g, "productos", db => db.Productos, p => p.Nombre,
            dto => dto.Nombre,
            (dto, e) =>
            {
                e.Nombre = dto.Nombre!.Trim();
                e.Codigo = string.IsNullOrWhiteSpace(dto.Codigo) ? null : dto.Codigo.Trim();
                return null;
            });

        Map<Exportador, ExportadorDto>(g, "exportadores", db => db.Exportadores, x => x.RazonSocial,
            dto => dto.RazonSocial,
            (dto, e) =>
            {
                var (cuit, error) = Cuit.Normalizar(dto.Cuit);
                if (error != null) return error;
                e.RazonSocial = dto.RazonSocial!.Trim();
                e.Cuit = cuit;
                return null;
            });

        Map<TaraConocida, TaraDto>(g, "taras", db => db.Taras, t => t.Identificador,
            dto => Patente.Normalizar(dto.Identificador),
            (dto, e) =>
            {
                if (dto.Kilogramos <= 0) return "La tara tiene que ser mayor a cero";
                if (e.Kilogramos != dto.Kilogramos || e.TomadaEn == default) e.TomadaEn = DateTimeOffset.UtcNow;
                e.Identificador = Patente.Normalizar(dto.Identificador);
                e.Kilogramos = dto.Kilogramos;
                e.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
                return null;
            });
    }

    /// <param name="nombre">Columna que identifica el registro: se busca, ordena y no se repite.</param>
    /// <param name="nombreDto">El nombre tal como se va a guardar (ya normalizado).</param>
    /// <param name="aplicar">Valida y copia el DTO a la entidad. Devuelve el error o null.</param>
    /// <param name="puedeBorrar">Devuelve el motivo por el que no se puede borrar, o null.</param>
    private static void Map<T, TDto>(
        RouteGroupBuilder g,
        string ruta,
        Func<BfsDbContext, DbSet<T>> set,
        Expression<Func<T, string>> nombre,
        Func<TDto, string?> nombreDto,
        Func<TDto, T, string?> aplicar,
        Func<BfsDbContext, T, Task<string?>>? puedeBorrar = null)
        where T : EntidadSync, new()
    {
        g.MapGet($"/{ruta}", async (string? q, BfsDbContext db) =>
        {
            var query = set(db).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(Contiene(nombre, q));
            return Results.Ok(await query.OrderBy(nombre).Take(MaxResultados).ToListAsync());
        }).RequireAuthorization(Permisos.CatalogosVer);

        g.MapPost($"/{ruta}", async (TDto dto, BfsDbContext db) =>
        {
            var valor = nombreDto(dto)?.Trim();
            if (string.IsNullOrWhiteSpace(valor)) return Results.BadRequest("Falta el nombre");

            // Si existía y se había borrado, se recupera con los datos nuevos (y su mismo Id).
            var existente = await set(db).IgnoreQueryFilters().Where(Igual(nombre, valor)).FirstOrDefaultAsync();
            if (existente is { Eliminado: false }) return Results.Conflict($"Ya existe \"{valor}\"");

            var entity = existente ?? new T { Id = Guid.NewGuid() };
            var error = aplicar(dto, entity);
            if (error != null) return Results.BadRequest(error);
            entity.Eliminado = false;
            if (existente == null) set(db).Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(entity);
        }).RequireAuthorization(Permisos.CatalogosEditar);

        g.MapPut($"/{ruta}/{{id:guid}}", async (Guid id, TDto dto, BfsDbContext db) =>
        {
            var entity = await set(db).FirstOrDefaultAsync(e => e.Id == id);
            if (entity == null) return Results.NotFound();
            var valor = nombreDto(dto)?.Trim();
            if (string.IsNullOrWhiteSpace(valor)) return Results.BadRequest("Falta el nombre");
            if (await set(db).IgnoreQueryFilters().Where(Igual(nombre, valor)).AnyAsync(e => e.Id != id))
            {
                return Results.Conflict($"Ya existe \"{valor}\"");
            }

            var error = aplicar(dto, entity);
            if (error != null) return Results.BadRequest(error);
            await db.SaveChangesAsync();
            return Results.Ok(entity);
        }).RequireAuthorization(Permisos.CatalogosEditar);

        g.MapDelete($"/{ruta}/{{id:guid}}", async (Guid id, BfsDbContext db) =>
        {
            var entity = await set(db).FirstOrDefaultAsync(e => e.Id == id);
            if (entity == null) return Results.NotFound();
            if (puedeBorrar != null && await puedeBorrar(db, entity) is { } motivo) return Results.Conflict(motivo);
            set(db).Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Permisos.CatalogosEditar);
    }

    // ToLower() se traduce igual en SQLite y PostgreSQL; el valor va como parámetro, no pegado en el SQL.
    private static readonly System.Reflection.MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly System.Reflection.MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static Expression<Func<T, bool>> Contiene<T>(Expression<Func<T, string>> nombre, string texto) =>
        Comparar(nombre, texto, (col, valor) => Expression.Call(col, Contains, valor));

    private static Expression<Func<T, bool>> Igual<T>(Expression<Func<T, string>> nombre, string texto) =>
        Comparar(nombre, texto, Expression.Equal);

    private static Expression<Func<T, bool>> Comparar<T>(Expression<Func<T, string>> nombre, string texto, Func<Expression, Expression, Expression> op)
    {
        var holder = new ValorBuscado(texto.Trim().ToLowerInvariant());
        var valor = Expression.Property(Expression.Constant(holder), nameof(ValorBuscado.Valor));
        var columna = Expression.Call(nombre.Body, ToLower);
        return Expression.Lambda<Func<T, bool>>(op(columna, valor), nombre.Parameters);
    }

    private sealed record ValorBuscado(string Valor);
}

public record ClienteDto(string? RazonSocial, string? Cuit);
public record TransporteDto(string? RazonSocial, string? Cuit, bool EsAta = false, string? CodigoAta = null);
public record ProductoDto(string? Nombre, string? Codigo);
public record ExportadorDto(string? RazonSocial, string? Cuit);
public record TaraDto(string? Identificador, int Kilogramos, string? Descripcion);
