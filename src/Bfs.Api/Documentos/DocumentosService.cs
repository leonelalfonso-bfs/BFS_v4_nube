using Bfs.Api.Config;
using Bfs.Api.Reportes;
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
    private readonly ReportesService _reportes;
    private readonly Nube.TenantActual _tenant;

    public DocumentosService(BfsDbContext db, ConfigService config, BfsPaths paths, BfsOptions options, ReportesService reportes, Nube.TenantActual tenant)
    {
        _tenant = tenant;
        _db = db;
        _config = config;
        _paths = paths;
        _options = options;
        _reportes = reportes;
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

    /// <summary>El Excel sale con los mismos filtros que la pantalla Pesadas.</summary>
    public async Task<byte[]> ExcelAsync(FiltroPesadas filtro)
    {
        var (desde, hasta) = (filtro.Desde, filtro.Hasta);
        var pesadas = await _reportes.Filtrar(filtro).OrderBy(p => p.EntradaEn).Take(MaxFilasExcel).ToListAsync();
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

    private async Task<byte[]?> Logo(string? archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo)) return null;
        var path = Path.Combine(_tenant.CarpetaBranding(_paths), Path.GetFileName(archivo));
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
