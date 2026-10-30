using Bfs.Api.Config;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Sistema;

public record Backup(string Archivo, long Bytes, DateTime Fecha);

public record NumeracionTicket(Guid EmpresaId, string Empresa, int Puesto, int Proximo, int MaximoUsado);

/// <summary>Pantalla Sistema de la 3.x: backups, restauración, espejo y número de ticket.</summary>
public class SistemaService
{
    public const int BackupsAutomaticosGuardados = 30;
    public const string PrefijoAutomatico = "auto-";

    private readonly BfsDbContext _db;
    private readonly BfsPaths _paths;
    private readonly ConfigService _config;

    public SistemaService(BfsDbContext db, BfsPaths paths, ConfigService config)
    {
        _db = db;
        _paths = paths;
        _config = config;
    }

    public IReadOnlyList<Backup> Backups() =>
        new DirectoryInfo(_paths.Backups).GetFiles("*.db")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new Backup(f.Name, f.Length, f.LastWriteTime))
            .ToList();

    public Backup CrearBackup(bool automatico = false)
    {
        var nombre = $"{(automatico ? PrefijoAutomatico : "")}bfs-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var destino = Path.Combine(_paths.Backups, nombre);
        ArchivosBase.Copiar(_paths.BaseSqlite, destino);
        if (automatico) PodarAutomaticos();
        var info = new FileInfo(destino);
        return new Backup(info.Name, info.Length, info.LastWriteTime);
    }

    /// <summary>Solo nombres de archivo de la carpeta de backups (nada de rutas que salgan de ahí).</summary>
    public string? RutaBackup(string archivo)
    {
        var nombre = Path.GetFileName(archivo);
        if (nombre != archivo || !nombre.EndsWith(".db", StringComparison.OrdinalIgnoreCase)) return null;
        var ruta = Path.Combine(_paths.Backups, nombre);
        return File.Exists(ruta) ? ruta : null;
    }

    /// <summary>Deja la base lista para restaurar en el próximo arranque.</summary>
    public async Task<string?> PrepararRestauracionAsync(Stream archivo)
    {
        var temporal = _paths.Restaurar + ".subida";
        await using (var f = File.Create(temporal))
        {
            await archivo.CopyToAsync(f);
        }
        var error = ArchivosBase.Validar(temporal);
        if (error != null)
        {
            File.Delete(temporal);
            return error;
        }
        File.Move(temporal, _paths.Restaurar, overwrite: true);
        return null;
    }

    public bool RestauracionPendiente => File.Exists(_paths.Restaurar);

    public void CancelarRestauracion()
    {
        if (File.Exists(_paths.Restaurar)) File.Delete(_paths.Restaurar);
    }

    /// <summary>Copia la base a la carpeta espejo. Devuelve el error, o null si salió bien.</summary>
    public async Task<string?> ActualizarEspejoAsync()
    {
        var c = await _config.GlobalAsync();
        var carpeta = c.GetValueOrDefault(ConfigKeys.EspejoCarpeta);
        if (string.IsNullOrWhiteSpace(carpeta)) return "No hay carpeta de espejo configurada";

        string? error = null;
        try
        {
            ArchivosBase.Copiar(_paths.BaseSqlite, Path.Combine(carpeta, "bfs-espejo.db"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            error = $"No se pudo copiar a {carpeta}: {ex.Message}";
        }
        await _config.GuardarAsync(new Dictionary<string, string>
        {
            [ConfigKeys.EspejoUltimo] = error == null ? DateTimeOffset.UtcNow.ToString("O") : c.GetValueOrDefault(ConfigKeys.EspejoUltimo, ""),
            [ConfigKeys.EspejoError] = error ?? ""
        });
        return error;
    }

    /// <summary>Por empresa, el contador de este puesto y el número más alto que ya se usó con ese prefijo.</summary>
    public async Task<IReadOnlyList<NumeracionTicket>> NumeracionAsync()
    {
        var puesto = await Puesto();
        var empresas = await _db.Empresas.AsNoTracking().OrderBy(e => e.Nombre).ToListAsync();
        var contadores = await _db.ContadoresTicket.AsNoTracking().Where(c => c.Puesto == puesto).ToListAsync();
        var lista = new List<NumeracionTicket>();
        foreach (var e in empresas)
        {
            var maximo = await MaximoUsado(e.Id, puesto);
            var proximo = contadores.FirstOrDefault(c => c.EmpresaId == e.Id)?.Proximo ?? (puesto == 1 ? Math.Max(1, e.ProximoTicket) : 1);
            lista.Add(new NumeracionTicket(e.Id, e.Nombre, puesto, Math.Max(proximo, maximo + 1), maximo));
        }
        return lista;
    }

    /// <summary>Fija el próximo número. No puede ser uno ya usado: chocaría con un ticket existente.</summary>
    public async Task<string?> FijarProximoAsync(Guid empresaId, int proximo)
    {
        if (proximo < 1 || proximo > 999_999) return "El número tiene que estar entre 1 y 999999";
        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId);
        if (empresa == null) return "Empresa inexistente";
        var puesto = await Puesto();
        var maximo = await MaximoUsado(empresaId, puesto);
        if (proximo <= maximo) return $"Ya existe el ticket {Tickets.Formatear(puesto, maximo)}: el próximo tiene que ser mayor a {maximo}";

        var contador = await _db.ContadoresTicket.FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.Puesto == puesto);
        if (contador == null)
        {
            contador = new ContadorTicket { Id = Guid.NewGuid(), EmpresaId = empresaId, Puesto = puesto };
            _db.ContadoresTicket.Add(contador);
        }
        contador.Proximo = proximo;
        await _db.SaveChangesAsync();
        return null;
    }

    private async Task<int> Puesto()
    {
        var c = await _config.GlobalAsync();
        return int.TryParse(c.GetValueOrDefault(ConfigKeys.Puesto), out var p) && p is >= 1 and <= 99 ? p : 1;
    }

    /// <summary>Tickets "PP-NNNNNN" de este puesto; en el puesto 1 también los numéricos importados de la 3.x.</summary>
    private async Task<int> MaximoUsado(Guid empresaId, int puesto)
    {
        var prefijo = $"{puesto:D2}-";
        var tickets = await _db.Pesadas.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.EmpresaId == empresaId)
            .Select(p => p.TicketNumero)
            .ToListAsync();
        return tickets
            .Select(t => t.StartsWith(prefijo, StringComparison.Ordinal) ? t[prefijo.Length..] : puesto == 1 && t.All(char.IsDigit) ? t : null)
            .Select(n => int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private void PodarAutomaticos()
    {
        foreach (var viejo in new DirectoryInfo(_paths.Backups).GetFiles(PrefijoAutomatico + "*.db")
                     .OrderByDescending(f => f.LastWriteTime).Skip(BackupsAutomaticosGuardados))
        {
            viejo.Delete();
        }
    }
}
