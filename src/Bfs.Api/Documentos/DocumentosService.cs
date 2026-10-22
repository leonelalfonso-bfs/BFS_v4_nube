using Bfs.Api.Config;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Documents;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Documentos;

/// <summary>Junta de la base todo lo que va en un ticket o en el Excel.</summary>
public class DocumentosService
{
    public const int MaxFilasExcel = 50_000;

    private readonly BfsDbContext _db;
    private readonly ConfigService _config;
    private readonly BfsPaths _paths;
    private readonly BfsOptions _options;

    public DocumentosService(BfsDbContext db, ConfigService config, BfsPaths paths, BfsOptions options)
    {
        _db = db;
        _config = config;
        _paths = paths;
        _options = options;
    }

    public async Task<TicketDatos?> TicketAsync(Guid pesadaId)
    {
        var p = await _db.Pesadas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pesadaId);
        if (p == null) return null;
        var c = await _config.GlobalAsync();
        var e = await _db.Empresas.AsNoTracking().FirstOrDefaultAsync(x => x.Id == p.EmpresaId);

        var clienteCuit = p.ClienteId.HasValue
            ? await _db.Clientes.AsNoTracking().Where(x => x.Id == p.ClienteId).Select(x => x.Cuit).FirstOrDefaultAsync()
            : await PorNombre(_db.Clientes, x => x.RazonSocial, p.Cliente, x => x.Cuit);
        var transporteCuit = await PorNombre(_db.Transportes, x => x.RazonSocial, p.Transporte, x => x.Cuit);
        var productoCodigo = await PorNombre(_db.Productos, x => x.Nombre, p.Producto, x => x.Codigo);

        AduanaTicket? aduana = null;
        if (p.TipoOperacion == TiposOperacion.Aduana)
        {
            var vence = DateOnly.TryParse(c.GetValueOrDefault(ConfigKeys.AduanaCertificado1Vence), out var v) ? v.ToString("dd/MM/yyyy") : null;
            aduana = new AduanaTicket(c.GetValueOrDefault(ConfigKeys.AduanaCodigo), c.GetValueOrDefault(ConfigKeys.AduanaLot),
                c.GetValueOrDefault(ConfigKeys.AduanaCertificado1), vence);
        }

        return new TicketDatos(
            p,
            new EmpresaTicket(e?.Nombre ?? c.GetValueOrDefault(ConfigKeys.ClienteNombre, ""), e?.Cuit, e?.Direccion, e?.Localidad, e?.Provincia, e?.Telefono, e?.Email),
            c.GetValueOrDefault(ConfigKeys.TicketTitulo, "Comprobante de pesaje"),
            c.GetValueOrDefault(ConfigKeys.TicketPie),
            await Logo(c.GetValueOrDefault(ConfigKeys.LogoFile)),
            _options.HoraLocal(p.EntradaEn),
            p.SalidaEn.HasValue ? _options.HoraLocal(p.SalidaEn.Value) : null,
            clienteCuit,
            transporteCuit,
            productoCodigo,
            aduana);
    }

    /// <param name="hasta">Inclusive (todo ese día).</param>
    public async Task<byte[]> ExcelAsync(DateOnly? desde, DateOnly? hasta, Guid? empresaId, Guid? soloCliente)
    {
        var q = _db.Pesadas.AsNoTracking();
        if (empresaId.HasValue) q = q.Where(p => p.EmpresaId == empresaId);
        if (soloCliente.HasValue) q = q.Where(p => p.ClienteId == soloCliente);
        if (desde.HasValue) q = q.Where(p => p.EntradaEn >= Inicio(desde.Value));
        if (hasta.HasValue) q = q.Where(p => p.EntradaEn < Inicio(hasta.Value.AddDays(1)));

        var pesadas = await q.OrderBy(p => p.EntradaEn).Take(MaxFilasExcel).ToListAsync();
        var empresas = await _db.Empresas.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Nombre);
        var filas = pesadas.Select(p => new FilaExcel(
            p,
            empresas.GetValueOrDefault(p.EmpresaId, ""),
            _options.HoraLocal(p.EntradaEn),
            p.SalidaEn.HasValue ? _options.HoraLocal(p.SalidaEn.Value) : null)).ToList();

        var rango = (desde, hasta) switch
        {
            (null, null) => "todas",
            ({ } d, null) => $"desde {d:dd/MM/yyyy}",
            (null, { } h) => $"hasta {h:dd/MM/yyyy}",
            ({ } d, { } h) => $"del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}"
        };
        return PesadasExcel.Generar(filas, $"Pesadas {rango}");
    }

    /// <summary>Medianoche de ese día en la zona de la planta, en UTC.</summary>
    private DateTimeOffset Inicio(DateOnly dia)
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

    private async Task<byte[]?> Logo(string? archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo)) return null;
        var path = Path.Combine(_paths.Branding, Path.GetFileName(archivo));
        // QuestPDF no dibuja SVG como imagen: el logo en SVG solo se usa en la pantalla.
        return File.Exists(path) && !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? await File.ReadAllBytesAsync(path) : null;
    }

    private static async Task<string?> PorNombre<T>(IQueryable<T> set, System.Linq.Expressions.Expression<Func<T, string>> nombre,
        string? valor, Func<T, string?> dato) where T : EntidadSync
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var buscado = valor.Trim().ToLower();
        var param = nombre.Parameters[0];
        var cond = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(
            System.Linq.Expressions.Expression.Equal(
                System.Linq.Expressions.Expression.Call(nombre.Body, typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!),
                System.Linq.Expressions.Expression.Constant(buscado)),
            param);
        var item = await set.AsNoTracking().FirstOrDefaultAsync(cond);
        return item == null ? null : dato(item);
    }
}
