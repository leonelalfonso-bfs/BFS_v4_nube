using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Config;

/// <summary>Configuración global (EmpresaId nulo) guardada como clave/valor.</summary>
public class ConfigService
{
    private readonly BfsDbContext _db;

    public ConfigService(BfsDbContext db)
    {
        _db = db;
    }

    public Dictionary<string, string> Global() =>
        _db.Configuraciones.AsNoTracking().Where(c => c.EmpresaId == null).ToDictionary(c => c.Clave, c => c.Valor);

    public Task<Dictionary<string, string>> GlobalAsync(CancellationToken ct = default) =>
        _db.Configuraciones.AsNoTracking().Where(c => c.EmpresaId == null).ToDictionaryAsync(c => c.Clave, c => c.Valor, ct);

    public Task<string?> ValorAsync(string clave) =>
        _db.Configuraciones.Where(c => c.Clave == clave && c.EmpresaId == null).Select(c => c.Valor).FirstOrDefaultAsync();

    public async Task GuardarAsync(IEnumerable<KeyValuePair<string, string>> valores)
    {
        foreach (var (clave, valor) in valores)
        {
            // Los secretos se muestran enmascarados: si vuelven así, no se tocan.
            if (clave == ConfigKeys.WormsApiKey && valor is "********" or "") continue;
            if (clave == ConfigKeys.ClaveManualHash && valor == "********") continue;

            var row = await _db.Configuraciones.FirstOrDefaultAsync(c => c.Clave == clave && c.EmpresaId == null);
            if (row == null)
            {
                _db.Configuraciones.Add(new Configuracion { Id = Guid.NewGuid(), Clave = clave, Valor = valor });
            }
            else
            {
                row.Valor = valor;
            }
        }
        await _db.SaveChangesAsync();
    }

}
