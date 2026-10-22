using System.Globalization;
using System.Text.RegularExpressions;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Scale.Tramas;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Balanzas;

public static partial class BalanzaEndpoints
{
    public static readonly int[] BaudiosValidos = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];
    public static readonly string[] ParidadesValidas = ["None", "Even", "Odd", "Mark", "Space"];

    /// <summary>Lee las balanzas activas de la base y reabre los puertos.</summary>
    public static async Task AplicarAsync(BfsDbContext db, IBalanzas balanzas)
    {
        var activas = await db.Balanzas.AsNoTracking().Where(b => b.Activa).OrderBy(b => b.Orden).ThenBy(b => b.Nombre).ToListAsync();
        balanzas.Aplicar(activas.Select(b => b.ToConfig()).ToList());
    }

    public static void MapBalanzaEndpoints(this RouteGroupBuilder api)
    {
        // Para el pesaje.
        api.MapGet("/balanzas/lecturas", (IBalanzas balanzas) => Results.Ok(balanzas.Lecturas))
            .RequireAuthorization(Permisos.PesajeOperar);
        api.MapGet("/scale/peso", (IBalanzas balanzas) => Results.Ok(balanzas.Lectura()))
            .RequireAuthorization(Permisos.PesajeOperar);

        // Para BFS al instalar.
        var g = api.MapGroup("/balanzas").RequireAuthorization(Permisos.ConfigBfs);

        g.MapGet("", async (BfsDbContext db, IBalanzas balanzas) =>
        {
            var lista = await db.Balanzas.AsNoTracking().OrderBy(b => b.Orden).ThenBy(b => b.Nombre).ToListAsync();
            var lecturas = balanzas.Lecturas.ToDictionary(l => l.BalanzaId);
            return Results.Ok(lista.Select(b => new { balanza = b, lectura = lecturas.GetValueOrDefault(b.Id) }));
        });

        g.MapGet("/puertos", (IBalanzas balanzas) => Results.Ok(balanzas.Puertos()));

        g.MapGet("/{id:guid}/tramas", (Guid id, IBalanzas balanzas) => Results.Ok(balanzas.UltimasTramas(id)));

        // Prueba la extracción sobre una trama pegada (acepta \x02 y similares) sin tocar la balanza.
        g.MapPost("/probar", (ProbarExtraccion req) =>
        {
            if (!string.IsNullOrWhiteSpace(req.Extraccion) && ExtractorPeso.Rango(req.Extraccion) == null)
            {
                return Results.BadRequest("La extracción tiene que ser inicio:fin, por ejemplo 5:10");
            }
            var trama = Desescapar(req.Trama ?? "");
            return Results.Ok(new { kilogramos = ExtractorPeso.Extraer(trama, req.Extraccion) });
        });

        g.MapPost("", async (BalanzaDto dto, BfsDbContext db, IBalanzas balanzas) =>
        {
            var entity = new Balanza { Id = Guid.NewGuid() };
            var error = await Validar(db, dto, entity);
            if (error != null) return error;
            db.Balanzas.Add(entity);
            await db.SaveChangesAsync();
            await AplicarAsync(db, balanzas);
            return Results.Ok(entity);
        });

        g.MapPut("/{id:guid}", async (Guid id, BalanzaDto dto, BfsDbContext db, IBalanzas balanzas) =>
        {
            var entity = await db.Balanzas.FirstOrDefaultAsync(b => b.Id == id);
            if (entity == null) return Results.NotFound();
            var error = await Validar(db, dto, entity);
            if (error != null) return error;
            await db.SaveChangesAsync();
            await AplicarAsync(db, balanzas);
            return Results.Ok(entity);
        });

        g.MapDelete("/{id:guid}", async (Guid id, BfsDbContext db, IBalanzas balanzas) =>
        {
            var entity = await db.Balanzas.FirstOrDefaultAsync(b => b.Id == id);
            if (entity == null) return Results.NotFound();
            db.Balanzas.Remove(entity);
            await db.SaveChangesAsync();
            await AplicarAsync(db, balanzas);
            return Results.NoContent();
        });

        g.MapPost("/reconectar", async (BfsDbContext db, IBalanzas balanzas) =>
        {
            await AplicarAsync(db, balanzas);
            return Results.Ok(balanzas.Lecturas);
        });

        g.MapGet("/modelos", async (BfsDbContext db) =>
            Results.Ok(await db.ModelosBalanza.AsNoTracking().OrderBy(m => m.Nombre).ToListAsync()));

        g.MapPost("/modelos", async (ModeloDto dto, BfsDbContext db) =>
        {
            var entity = new ModeloBalanza { Id = Guid.NewGuid() };
            var error = await ValidarModelo(db, dto, entity);
            if (error != null) return error;
            db.ModelosBalanza.Add(entity);
            await db.SaveChangesAsync();
            return Results.Ok(entity);
        });

        g.MapPut("/modelos/{id:guid}", async (Guid id, ModeloDto dto, BfsDbContext db) =>
        {
            var entity = await db.ModelosBalanza.FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null) return Results.NotFound();
            var error = await ValidarModelo(db, dto, entity);
            if (error != null) return error;
            await db.SaveChangesAsync();
            return Results.Ok(entity);
        });

        g.MapDelete("/modelos/{id:guid}", async (Guid id, BfsDbContext db) =>
        {
            var entity = await db.ModelosBalanza.FirstOrDefaultAsync(m => m.Id == id);
            if (entity == null) return Results.NotFound();
            db.ModelosBalanza.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static async Task<IResult?> Validar(BfsDbContext db, BalanzaDto dto, Balanza e)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre)) return Results.BadRequest("Falta el nombre");
        if (dto.Modo is not (ModosBalanza.Serie or ModosBalanza.Simulada)) return Results.BadRequest("Modo inválido");
        var puerto = dto.Puerto?.Trim().ToUpperInvariant() ?? "";
        if (dto.Modo == ModosBalanza.Serie && puerto.Length == 0) return Results.BadRequest("Falta el puerto");
        if (ValidarParametros(dto.Baudios, dto.DataBits, dto.Paridad, dto.StopBits, dto.Protocolo, dto.Extraccion) is { } error)
        {
            return Results.BadRequest(error);
        }

        var buscado = nombre.ToLower();
        if (await db.Balanzas.IgnoreQueryFilters().AnyAsync(b => b.Id != e.Id && b.Nombre.ToLower() == buscado))
        {
            return Results.Conflict($"Ya existe la balanza \"{nombre}\"");
        }
        var activa = dto.Activa ?? true;
        if (activa && dto.Modo == ModosBalanza.Serie &&
            await db.Balanzas.AnyAsync(b => b.Id != e.Id && b.Activa && b.Modo == ModosBalanza.Serie && b.Puerto == puerto))
        {
            return Results.Conflict($"{puerto} ya lo usa otra balanza");
        }

        e.Nombre = nombre;
        e.Modo = dto.Modo;
        e.Puerto = puerto;
        e.Modelo = string.IsNullOrWhiteSpace(dto.Modelo) ? null : dto.Modelo.Trim();
        e.Baudios = dto.Baudios;
        e.DataBits = dto.DataBits;
        e.Paridad = dto.Paridad;
        e.StopBits = dto.StopBits;
        e.Protocolo = dto.Protocolo;
        e.Extraccion = string.IsNullOrWhiteSpace(dto.Extraccion) ? null : dto.Extraccion.Trim();
        // Sin orden explícito, una balanza nueva va al final: la primera es la que usa el pesaje por defecto.
        if (dto.Orden > 0) e.Orden = dto.Orden;
        else if (e.Orden == 0) e.Orden = (await db.Balanzas.MaxAsync(b => (int?)b.Orden) ?? 0) + 1;
        e.Activa = activa;
        return null;
    }

    private static async Task<IResult?> ValidarModelo(BfsDbContext db, ModeloDto dto, ModeloBalanza e)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre)) return Results.BadRequest("Falta el nombre");
        if (ValidarParametros(dto.Baudios, dto.DataBits, dto.Paridad, dto.StopBits, dto.Protocolo, dto.Extraccion) is { } error)
        {
            return Results.BadRequest(error);
        }
        var buscado = nombre.ToLower();
        if (await db.ModelosBalanza.IgnoreQueryFilters().AnyAsync(m => m.Id != e.Id && m.Nombre.ToLower() == buscado))
        {
            return Results.Conflict($"Ya existe el modelo \"{nombre}\"");
        }

        e.Nombre = nombre;
        e.Baudios = dto.Baudios;
        e.DataBits = dto.DataBits;
        e.Paridad = dto.Paridad;
        e.StopBits = dto.StopBits;
        e.Protocolo = dto.Protocolo;
        e.Extraccion = string.IsNullOrWhiteSpace(dto.Extraccion) ? null : dto.Extraccion.Trim();
        return null;
    }

    private static string? ValidarParametros(int baudios, int dataBits, string paridad, int stopBits, string protocolo, string? extraccion)
    {
        if (!BaudiosValidos.Contains(baudios)) return "Baudios inválidos";
        if (dataBits is not (7 or 8)) return "Data bits tiene que ser 7 u 8";
        if (!ParidadesValidas.Contains(paridad)) return "Paridad inválida";
        if (stopBits is not (1 or 2)) return "Stop bits tiene que ser 1 o 2";
        if (!ProtocolosBalanza.Todos.Contains(protocolo)) return "Protocolo inválido (STX o Line)";
        if (!string.IsNullOrWhiteSpace(extraccion) && ExtractorPeso.Rango(extraccion) == null)
        {
            return "La extracción tiene que ser inicio:fin, por ejemplo 5:10";
        }
        return null;
    }

    /// <summary>"\x02 12480\x03" → los caracteres reales. También acepta \r y \n.</summary>
    public static string Desescapar(string trama) =>
        Escape().Replace(trama.Replace("\\r", "\r").Replace("\\n", "\n"),
            m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber)).ToString());

    [GeneratedRegex(@"\\x([0-9A-Fa-f]{2})")]
    private static partial Regex Escape();
}

public record BalanzaDto(string? Nombre, string Modo, string? Puerto, string? Modelo, int Baudios, int DataBits,
    string Paridad, int StopBits, string Protocolo, string? Extraccion, int Orden = 0, bool? Activa = true);
public record ModeloDto(string? Nombre, int Baudios, int DataBits, string Paridad, int StopBits, string Protocolo, string? Extraccion);
public record ProbarExtraccion(string? Trama, string? Extraccion);
