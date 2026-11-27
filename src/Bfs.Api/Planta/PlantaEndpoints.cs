using Bfs.Api.Config;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Planta;

/// <summary>
/// Datos de la planta. Las funciones (aduana, tara conocida, térmica...) las habilita BFS desde /config;
/// acá el cliente carga sus empresas, los datos de aduana y la clave de ingreso manual.
/// </summary>
public static class PlantaEndpoints
{
    public const int LargoMinimoClaveManual = 4;

    public static void MapPlantaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/empresas", async (BfsDbContext db) =>
            Results.Ok(await db.Empresas.OrderBy(e => e.Nombre).ToListAsync()));

        api.MapPost("/empresas", async (EmpresaDto dto, BfsDbContext db, BfsOptions opciones) =>
        {
            if (opciones.Edicion == Edicion.Estandar && await db.Empresas.AnyAsync())
            {
                return Results.BadRequest("La edición Estándar trabaja con una sola empresa");
            }
            var empresa = new Empresa { Id = Guid.NewGuid() };
            var error = await Aplicar(db, dto, empresa);
            if (error != null) return error;
            db.Empresas.Add(empresa);
            await db.SaveChangesAsync();
            return Results.Ok(empresa);
        }).RequireAuthorization(Permisos.EmpresasGestionar);

        api.MapPut("/empresas/{id:guid}", async (Guid id, EmpresaDto dto, BfsDbContext db) =>
        {
            var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == id);
            if (empresa == null) return Results.NotFound();
            var error = await Aplicar(db, dto, empresa);
            if (error != null) return error;
            await db.SaveChangesAsync();
            return Results.Ok(empresa);
        }).RequireAuthorization(Permisos.ConfigPlanta);

        // Lo que necesita saber la pantalla de pesaje. Sin datos sensibles: la clave manual solo dice si existe.
        api.MapGet("/funciones", async (ConfigService config) =>
        {
            var c = await config.GlobalAsync();
            bool Si(string k, string def = "false") => c.GetValueOrDefault(k, def) == "true";
            return Results.Ok(new
            {
                aduana = Si(ConfigKeys.Aduana),
                contenedores = Si(ConfigKeys.Contenedores),
                taraConocida = Si(ConfigKeys.TaraConocida),
                taraPallets = Si(ConfigKeys.TaraPallets, "true"),
                termica = Si(ConfigKeys.Termica),
                termicaImpresora = c.GetValueOrDefault(ConfigKeys.TermicaImpresora, ""),
                termicaAncho = c.GetValueOrDefault(ConfigKeys.TermicaAncho, "80"),
                ingresoManual = Si(ConfigKeys.IngresoManual, "true"),
                claveManual = !string.IsNullOrEmpty(c.GetValueOrDefault(ConfigKeys.ClaveManualHash)),
                claveCorreccion = !string.IsNullOrEmpty(c.GetValueOrDefault(ConfigKeys.ClaveCorreccionHash)),
                claveBorrado = !string.IsNullOrEmpty(c.GetValueOrDefault(ConfigKeys.ClaveBorradoHash)),
                exigirEstable = Si(ConfigKeys.ExigirEstable, "true"),
                aduanaLot = c.GetValueOrDefault(ConfigKeys.AduanaLot, ""),
                aduanaEscenario = c.GetValueOrDefault(ConfigKeys.AduanaEscenario, ""),
                pesajePartes = Si(ConfigKeys.PesajePartes),
                pesajeEjes = Si(ConfigKeys.PesajeEjes),
                ejesPasadas = c.GetValueOrDefault(ConfigKeys.EjesPasadas, "dos"),
                ejesExceso = c.GetValueOrDefault(ConfigKeys.EjesExceso, "aviso"),
                ejesLimiteTotal = int.TryParse(c.GetValueOrDefault(ConfigKeys.EjesLimiteTotal), out var lt) && lt > 0 ? lt : TiposEje.LimiteTotalPorDefecto,
                tiposEje = TiposEje.Todos.Select(t => new { t.Id, t.Nombre, t.Ejes, limite = TiposEje.Limites(c.GetValueOrDefault(ConfigKeys.EjesLimites))[t.Id] }),
                puesto = int.TryParse(c.GetValueOrDefault(ConfigKeys.Puesto), out var puesto) ? puesto : 1,
                // Automatización (semáforos, barreras, sensores y cámaras): la pantalla de pesaje solo la muestra si está habilitada.
                automatizacion = ConfigAutomatizacion.Parse(c.GetValueOrDefault(ConfigKeys.AutomatizacionJson)).Habilitada
            });
        });

        api.MapGet("/planta/aduana", async (ConfigService config) =>
        {
            var c = await config.GlobalAsync();
            return Results.Ok(new AduanaDto(
                c.GetValueOrDefault(ConfigKeys.AduanaCodigo),
                c.GetValueOrDefault(ConfigKeys.AduanaLot),
                c.GetValueOrDefault(ConfigKeys.AduanaCertificado1),
                Fecha(c.GetValueOrDefault(ConfigKeys.AduanaCertificado1Vence)),
                c.GetValueOrDefault(ConfigKeys.AduanaCertificado2),
                Fecha(c.GetValueOrDefault(ConfigKeys.AduanaCertificado2Vence)),
                c.GetValueOrDefault(ConfigKeys.AduanaHabilitacion)));
        }).RequireAuthorization(Permisos.ConfigPlanta);

        api.MapPut("/planta/aduana", async (AduanaDto dto, ConfigService config) =>
        {
            var valores = new Dictionary<string, string>
            {
                [ConfigKeys.AduanaCodigo] = dto.Codigo?.Trim() ?? "",
                [ConfigKeys.AduanaLot] = dto.Lot?.Trim() ?? "",
                [ConfigKeys.AduanaCertificado1] = dto.Certificado1?.Trim() ?? "",
                [ConfigKeys.AduanaCertificado1Vence] = dto.Certificado1Vence?.ToString("yyyy-MM-dd") ?? "",
                [ConfigKeys.AduanaCertificado2] = dto.Certificado2?.Trim() ?? "",
                [ConfigKeys.AduanaCertificado2Vence] = dto.Certificado2Vence?.ToString("yyyy-MM-dd") ?? ""
            };
            if (dto.Habilitacion != null) valores[ConfigKeys.AduanaHabilitacion] = dto.Habilitacion.Trim();
            await config.GuardarAsync(valores);
            return Results.Ok(dto);
        }).RequireAuthorization(Permisos.ConfigPlanta);

        // Vacía = sin clave. Se guarda con hash; la 3.x la tenía en texto plano.
        api.MapPut("/planta/clave-manual", async (ClaveManualDto dto, ConfigService config, PasswordHasher<Usuario> hasher) =>
            await GuardarClave(dto.Clave, ConfigKeys.ClaveManualHash, config, hasher) is { } hash
                ? Results.Ok(new { claveManual = hash.Length > 0 })
                : Results.BadRequest($"La clave tiene que tener al menos {LargoMinimoClaveManual} caracteres"))
            .RequireAuthorization(Permisos.ConfigPlanta);

        api.MapPut("/planta/clave-correccion", async (ClaveManualDto dto, ConfigService config, PasswordHasher<Usuario> hasher) =>
            await GuardarClave(dto.Clave, ConfigKeys.ClaveCorreccionHash, config, hasher) is { } hash
                ? Results.Ok(new { claveCorreccion = hash.Length > 0 })
                : Results.BadRequest($"La clave tiene que tener al menos {LargoMinimoClaveManual} caracteres"))
            .RequireAuthorization(Permisos.ConfigPlanta);

        // Borrar un registro de un catálogo (el operador los carga y edita; borrar puede pedir esta clave).
        api.MapPut("/planta/clave-borrado", async (ClaveManualDto dto, ConfigService config, PasswordHasher<Usuario> hasher) =>
            await GuardarClave(dto.Clave, ConfigKeys.ClaveBorradoHash, config, hasher) is { } hash
                ? Results.Ok(new { claveBorrado = hash.Length > 0 })
                : Results.BadRequest($"La clave tiene que tener al menos {LargoMinimoClaveManual} caracteres"))
            .RequireAuthorization(Permisos.ConfigPlanta);

        api.MapGet("/vehiculos/{patente}", async (string patente, BfsDbContext db) =>
        {
            var p = Patente.Normalizar(patente);
            var datos = await db.DatosVehiculos.AsNoTracking().FirstOrDefaultAsync(d => d.PatenteChasis == p);
            var tara = await db.Taras.AsNoTracking().FirstOrDefaultAsync(t => t.Identificador == p);
            return datos == null && tara == null
                ? Results.NotFound()
                : Results.Ok(new { datos, tara });
        }).RequireAuthorization(Permisos.PesajeOperar);
    }

    /// <summary>Guarda el hash de la clave (vacía = sin clave). Null si es demasiado corta.</summary>
    private static async Task<string?> GuardarClave(string? clave, string configKey, ConfigService config, PasswordHasher<Usuario> hasher)
    {
        clave ??= "";
        if (clave.Length is > 0 and < LargoMinimoClaveManual) return null;
        var hash = clave.Length == 0 ? "" : hasher.HashPassword(new Usuario(), clave);
        await config.GuardarAsync(new Dictionary<string, string> { [configKey] = hash });
        return hash;
    }

    private static DateOnly? Fecha(string? s) => DateOnly.TryParse(s, out var d) ? d : null;

    private static async Task<IResult?> Aplicar(BfsDbContext db, EmpresaDto dto, Empresa e)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre)) return Results.BadRequest("Falta la razón social");
        var (cuit, error) = Cuit.Normalizar(dto.Cuit);
        if (error != null) return Results.BadRequest(error);
        var buscado = nombre.ToLower();
        if (await db.Empresas.IgnoreQueryFilters().AnyAsync(x => x.Id != e.Id && x.Nombre.ToLower() == buscado))
        {
            return Results.Conflict($"Ya existe la empresa \"{nombre}\"");
        }

        e.Nombre = nombre;
        e.Cuit = cuit;
        e.Direccion = Limpio(dto.Direccion);
        e.Localidad = Limpio(dto.Localidad);
        e.Provincia = Limpio(dto.Provincia);
        e.Telefono = Limpio(dto.Telefono);
        e.Email = Limpio(dto.Email);
        if (dto.Activa.HasValue) e.Activa = dto.Activa.Value;
        return null;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public record EmpresaDto(string? Nombre, string? Cuit, string? Direccion, string? Localidad, string? Provincia,
    string? Telefono, string? Email, bool? Activa = null);
public record AduanaDto(string? Codigo, string? Lot, string? Certificado1, DateOnly? Certificado1Vence,
    string? Certificado2, DateOnly? Certificado2Vence, string? Habilitacion = null);
public record ClaveManualDto(string? Clave);
