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
    private readonly Automatizacion.FotosPesadas? _fotos;

    public DocumentosService(BfsDbContext db, ConfigService config, BfsPaths paths, BfsOptions options, ReportesService reportes, Nube.TenantActual tenant,
        Automatizacion.FotosPesadas? fotos = null)
    {
        _fotos = fotos;
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
        var comprobanteLabel = FieldCatalog.Parse(c.GetValueOrDefault(ConfigKeys.FieldsJson))
            .FirstOrDefault(f => f.Key == "comprobante")?.Label ?? "Remito";

        AduanaTicket? aduana = null;
        string? exportadorCuit = null;
        string? ataCuit = null;
        string? ataCodigo = null;
        if (p.TipoOperacion == TiposOperacion.Aduana)
        {
            exportadorCuit = !string.IsNullOrWhiteSpace(p.Aduana?.ExportadorCuit)
                ? p.Aduana!.ExportadorCuit
                : await PorNombre(_db.Exportadores, x => x.RazonSocial, p.Aduana?.Exportador, x => x.Cuit);
            ataCuit = !string.IsNullOrWhiteSpace(p.Aduana?.AtaCuit)
                ? p.Aduana!.AtaCuit
                : await PorNombre(_db.Transportes, x => x.RazonSocial, p.Aduana?.Ata, x => x.Cuit);
            ataCodigo = await PorNombre(_db.Transportes, x => x.RazonSocial, p.Aduana?.Ata, x => x.CodigoAta);

            var (certificado, venceRaw) = await CertificadoDeBalanzaAsync(c, p.BalanzaSalida ?? p.Balanza);
            var vence = DateOnly.TryParse(venceRaw, out var v) ? v.ToString("dd/MM/yyyy") : null;
            aduana = new AduanaTicket(
                c.GetValueOrDefault(ConfigKeys.AduanaCodigo),
                c.GetValueOrDefault(ConfigKeys.AduanaLot),
                certificado,
                vence,
                c.GetValueOrDefault(ConfigKeys.AduanaHabilitacion));
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
            aduana)
        {
            ExportadorCuit = exportadorCuit,
            AtaCuit = ataCuit,
            AtaCodigo = ataCodigo,
            ComprobanteLabel = comprobanteLabel,
            MarcarExcesoEjes = c.GetValueOrDefault(ConfigKeys.EjesExceso) == "ticket",
            // Las fotos están en el puesto que pesó; en la nube (o si se borraron) el ticket sale sin ellas.
            Fotos = _fotos == null ? [] : Automatizacion.AutomatizacionPesaje.Leer(p)
                .Select(f => _fotos.Leer(f) is { } datos ? new FotoTicket(f.Momento, f.Camara, datos) : null)
                .Where(f => f != null).Select(f => f!).ToList()
        };
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
        // Filtrado por empresa, el título lo dice: los informes de cada empresa quedan separados.
        var empresa = filtro.EmpresaId is { } id && empresas.TryGetValue(id, out var nombre) ? $" de {nombre}" : "";
        return PesadasExcel.Generar(filas, $"Pesadas{empresa} {rango}", _options.HoraLocal);
    }

    private async Task<byte[]?> Logo(string? archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo)) return null;
        var path = Path.Combine(_tenant.CarpetaBranding(_paths), Path.GetFileName(archivo));
        // QuestPDF no dibuja SVG como imagen: el logo en SVG solo se usa en la pantalla.
        return File.Exists(path) && !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? await File.ReadAllBytesAsync(path) : null;
    }

    private async Task<(string? Certificado, string? Vence)> CertificadoDeBalanzaAsync(Dictionary<string, string> c, string? nombreBalanza)
    {
        static string? NoVacio(string? a, string? b) => !string.IsNullOrWhiteSpace(a) ? a : (!string.IsNullOrWhiteSpace(b) ? b : null);

        var c1 = c.GetValueOrDefault(ConfigKeys.AduanaCertificado1);
        var v1 = c.GetValueOrDefault(ConfigKeys.AduanaCertificado1Vence);
        var c2 = c.GetValueOrDefault(ConfigKeys.AduanaCertificado2);
        var v2 = c.GetValueOrDefault(ConfigKeys.AduanaCertificado2Vence);

        var usarSegunda = false;
        if (!string.IsNullOrWhiteSpace(nombreBalanza))
        {
            var nombres = await _db.Balanzas.AsNoTracking().OrderBy(b => b.Orden).Select(b => b.Nombre).ToListAsync();
            var idx = nombres.FindIndex(n => string.Equals(n, nombreBalanza.Trim(), StringComparison.OrdinalIgnoreCase));
            usarSegunda = idx == 1;
        }

        return usarSegunda
            ? (NoVacio(c2, c1), NoVacio(v2, v1))
            : (NoVacio(c1, c2), NoVacio(v1, v2));
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
