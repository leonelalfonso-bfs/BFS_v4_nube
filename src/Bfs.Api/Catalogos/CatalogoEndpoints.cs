using System.Security.Claims;
using Bfs.Api.Auth;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Catalogos;

public static class CatalogoEndpoints
{
    public static void MapCatalogoEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/empresas", async (BfsDbContext db) =>
            Results.Ok(await db.Empresas.OrderBy(e => e.Nombre).ToListAsync()));

        api.MapPost("/empresas", async (Empresa dto, BfsDbContext db, ClaimsPrincipal user) =>
        {
            if (!user.EsVendor()) return Results.Forbid();
            dto.Id = Guid.NewGuid();
            db.Empresas.Add(dto);
            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });

        api.MapGet("/catalogos/{tipo}", async (string tipo, BfsDbContext db) => tipo switch
        {
            "clientes" => Results.Ok(await db.Clientes.OrderBy(x => x.RazonSocial).ToListAsync()),
            "productos" => Results.Ok(await db.Productos.OrderBy(x => x.Nombre).ToListAsync()),
            "transportes" => Results.Ok(await db.Transportes.OrderBy(x => x.RazonSocial).ToListAsync()),
            _ => Results.NotFound()
        });

        api.MapPost("/catalogos/clientes", (Cliente dto, BfsDbContext db) => Agregar(db, dto));
        api.MapPost("/catalogos/productos", (Producto dto, BfsDbContext db) => Agregar(db, dto));
        api.MapPost("/catalogos/transportes", (Transporte dto, BfsDbContext db) => Agregar(db, dto));
    }

    private static async Task<IResult> Agregar<T>(BfsDbContext db, T dto) where T : EntidadSync
    {
        dto.Id = Guid.NewGuid();
        db.Add(dto);
        await db.SaveChangesAsync();
        return Results.Ok(dto);
    }
}
