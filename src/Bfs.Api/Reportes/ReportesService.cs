using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Reportes;

/// <summary>Filtros de la pantalla Pesadas; los mismos sirven para el listado, los totales y el Excel.</summary>
/// <param name="Hasta">Inclusive (todo ese día).</param>
/// <param name="Texto">Busca en ticket, patente, chofer y remito.</param>
public record FiltroPesadas(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    Guid? EmpresaId = null,
    string? Cliente = null,
    string? Transporte = null,
    string? Producto = null,
    string? Texto = null,
    string? Estado = null,
    Guid? SoloCliente = null);

public record Totales(int Cantidad, int Cerradas, int Pendientes, int Anuladas, long Neto);

public record PaginaPesadas(IReadOnlyList<Pesada> Items, int Total, int Pagina, int Tamano, Totales Totales);

public record PuntoDia(DateOnly Fecha, int Cantidad, long Neto);

public record Ranking(string Nombre, int Cantidad, long Neto);

public record Resumen(Totales Hoy, Totales Periodo, int Dias, IReadOnlyList<PuntoDia> PorDia, IReadOnlyList<Ranking> TopClientes,
    IReadOnlyList<Ranking> TopProductos, int PendientesAhora);

public class ReportesService
{
    public const int TamanoMaximo = 500;

    private readonly BfsDbContext _db;
    private readonly BfsOptions _options;

    public ReportesService(BfsDbContext db, BfsOptions options)
    {
        _db = db;
        _options = options;
    }

    public IQueryable<Pesada> Filtrar(FiltroPesadas f)
    {
        var q = _db.Pesadas.AsNoTracking();
        if (f.SoloCliente.HasValue) q = q.Where(p => p.ClienteId == f.SoloCliente);
        if (f.EmpresaId.HasValue) q = q.Where(p => p.EmpresaId == f.EmpresaId);
        if (f.Desde.HasValue) q = q.Where(p => p.EntradaEn >= InicioDelDia(f.Desde.Value));
        if (f.Hasta.HasValue) q = q.Where(p => p.EntradaEn < InicioDelDia(f.Hasta.Value.AddDays(1)));
        if (!string.IsNullOrWhiteSpace(f.Estado))
        {
            var estado = f.Estado.Trim().ToUpperInvariant();
            q = q.Where(p => p.Estado == estado);
        }
        if (Valor(f.Cliente) is { } cliente) q = q.Where(p => p.Cliente != null && p.Cliente.ToLower().Contains(cliente));
        if (Valor(f.Transporte) is { } transporte) q = q.Where(p => p.Transporte != null && p.Transporte.ToLower().Contains(transporte));
        if (Valor(f.Producto) is { } producto) q = q.Where(p => p.Producto != null && p.Producto.ToLower().Contains(producto));
        if (Valor(f.Texto) is { } texto)
        {
            var patente = Patente.Normalizar(texto);
            q = q.Where(p =>
                p.TicketNumero.ToLower().Contains(texto)
                || (patente.Length > 0 && (p.PatenteChasis.Contains(patente) || (p.PatenteAcoplado != null && p.PatenteAcoplado.Contains(patente))))
                || (p.Conductor != null && p.Conductor.ToLower().Contains(texto))
                || (p.Comprobante != null && p.Comprobante.ToLower().Contains(texto)));
        }
        return q;
    }

    public async Task<PaginaPesadas> ListarAsync(FiltroPesadas f, int pagina, int tamano)
    {
        pagina = Math.Max(1, pagina);
        tamano = Math.Clamp(tamano, 1, TamanoMaximo);
        var q = Filtrar(f);
        var totales = await TotalesAsync(q);
        var items = await q.OrderByDescending(p => p.EntradaEn).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return new PaginaPesadas(items, totales.Cantidad, pagina, tamano, totales);
    }

    /// <summary>El neto suma solo las cerradas: una pendiente no tiene neto y una anulada no cuenta.</summary>
    private static async Task<Totales> TotalesAsync(IQueryable<Pesada> q)
    {
        var porEstado = await q.GroupBy(p => p.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count(), Neto = g.Sum(p => (long?)p.Neto) ?? 0 })
            .ToListAsync();
        int Cant(string e) => porEstado.FirstOrDefault(x => x.Estado == e)?.Cantidad ?? 0;
        return new Totales(porEstado.Sum(x => x.Cantidad), Cant(EstadosPesada.Salida), Cant(EstadosPesada.Entrada), Cant(EstadosPesada.Anulada),
            porEstado.FirstOrDefault(x => x.Estado == EstadosPesada.Salida)?.Neto ?? 0);
    }

    /// <summary>Tablero: hoy, los últimos <paramref name="dias"/> días por día, y los clientes y productos con más neto.</summary>
    public async Task<Resumen> ResumenAsync(int dias, Guid? soloCliente)
    {
        dias = Math.Clamp(dias, 1, 366);
        var hoy = DateOnly.FromDateTime(_options.HoraLocal(DateTimeOffset.UtcNow));
        var desde = hoy.AddDays(-(dias - 1));
        var periodo = Filtrar(new FiltroPesadas(Desde: desde, Hasta: hoy, SoloCliente: soloCliente));

        // Se trae lo mínimo y se agrupa en memoria: el día depende de la zona horaria de la planta.
        var filas = await periodo.Select(p => new Fila(p.EntradaEn, p.Estado, p.Neto, p.Cliente, p.Producto)).ToListAsync();
        var cerradas = filas.Where(f => f.Estado == EstadosPesada.Salida).ToList();
        static Totales Calcular(IEnumerable<Fila> xs)
        {
            var l = xs.ToList();
            return new Totales(l.Count, l.Count(x => x.Estado == EstadosPesada.Salida), l.Count(x => x.Estado == EstadosPesada.Entrada),
                l.Count(x => x.Estado == EstadosPesada.Anulada), l.Where(x => x.Estado == EstadosPesada.Salida).Sum(x => (long)(x.Neto ?? 0)));
        }

        var porDia = Enumerable.Range(0, dias).Select(i => desde.AddDays(i)).Select(d =>
        {
            var delDia = cerradas.Where(c => DateOnly.FromDateTime(_options.HoraLocal(c.EntradaEn)) == d).ToList();
            return new PuntoDia(d, delDia.Count, delDia.Sum(c => (long)(c.Neto ?? 0)));
        }).ToList();

        static List<Ranking> Top(IEnumerable<(string? Nombre, int? Neto)> xs) => xs
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Nombre) ? "(sin dato)" : x.Nombre.Trim())
            .Select(g => new Ranking(g.Key, g.Count(), g.Sum(x => (long)(x.Neto ?? 0))))
            .OrderByDescending(r => r.Neto).Take(5).ToList();

        var pendientes = await Filtrar(new FiltroPesadas(Estado: EstadosPesada.Entrada, SoloCliente: soloCliente)).CountAsync();
        return new Resumen(
            Calcular(filas.Where(f => DateOnly.FromDateTime(_options.HoraLocal(f.EntradaEn)) == hoy)),
            Calcular(filas),
            dias,
            porDia,
            Top(cerradas.Select(c => (c.Cliente, c.Neto))),
            Top(cerradas.Select(c => (c.Producto, c.Neto))),
            pendientes);
    }

    /// <summary>Medianoche de ese día en la zona de la planta, en UTC.</summary>
    public DateTimeOffset InicioDelDia(DateOnly dia)
    {
        var local = dia.ToDateTime(TimeOnly.MinValue);
        try
        {
            var zona = TimeZoneInfo.FindSystemTimeZoneById(_options.ZonaHoraria);
            return new DateTimeOffset(local, zona.GetUtcOffset(local)).ToUniversalTime();
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new DateTimeOffset(local).ToUniversalTime();
        }
    }

    private static string? Valor(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();

    private sealed record Fila(DateTimeOffset EntradaEn, string Estado, int? Neto, string? Cliente, string? Producto);
}
