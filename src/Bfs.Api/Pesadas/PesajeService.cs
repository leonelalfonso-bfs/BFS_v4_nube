using Bfs.Api.Config;
using Bfs.Api.Licencias;
using Bfs.Core;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Bfs.Integrations.Worms;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Api.Pesadas;

public record ResultadoPesaje(Pesada? Pesada, string? Error = null, bool NoEncontrada = false, bool Bloqueada = false)
{
    public static ResultadoPesaje Ok(Pesada p) => new(p);
    public static ResultadoPesaje Invalida(string error) => new(null, error);
    public static ResultadoPesaje Inexistente() => new(null, NoEncontrada: true);
    public static ResultadoPesaje Prohibida(string motivo) => new(null, motivo, Bloqueada: true);
}

public class PesajeService
{
    private readonly BfsDbContext _db;
    private readonly IScaleReader _scale;
    private readonly ConfigService _config;
    private readonly LicenciaService _licencia;

    public PesajeService(BfsDbContext db, IScaleReader scale, ConfigService config, LicenciaService licencia)
    {
        _db = db;
        _scale = scale;
        _config = config;
        _licencia = licencia;
    }

    /// <param name="soloCliente">Si viene, solo las pesadas de ese cliente (usuarios con rol cliente).</param>
    public Task<List<Pesada>> ListarAsync(Guid? empresaId, Guid? soloCliente = null)
    {
        var q = _db.Pesadas.AsQueryable();
        if (empresaId.HasValue) q = q.Where(p => p.EmpresaId == empresaId);
        if (soloCliente.HasValue) q = q.Where(p => p.ClienteId == soloCliente);
        return q.OrderByDescending(p => p.EntradaEn).Take(200).ToListAsync();
    }

    public async Task<ResultadoPesaje> RegistrarEntradaAsync(EntradaRequest req, string operador)
    {
        // Solo se bloquean las entradas: la salida de un camión que ya entró siempre se puede cerrar.
        var licencia = _licencia.Estado();
        if (!licencia.PuedePesar) return ResultadoPesaje.Prohibida(licencia.Mensaje);

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == req.EmpresaId);
        if (empresa == null) return ResultadoPesaje.Invalida("Empresa inexistente");

        var configs = await _config.GlobalAsync();
        var clienteId = await ResolverCliente(req.ClienteId, req.Cliente);
        if (req.ClienteId.HasValue && clienteId == null) return ResultadoPesaje.Invalida("Cliente inexistente");
        var ticket = empresa.ProximoTicket++;
        var pesada = new Pesada
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresa.Id,
            TicketNumero = ticket.ToString(),
            Tipo = string.IsNullOrWhiteSpace(req.Tipo) ? configs.GetValueOrDefault(ConfigKeys.WormsTipoDefault, "INGRESO") : req.Tipo,
            Estado = "ENTRADA",
            PatenteChasis = req.PatenteChasis.Replace(" ", ""),
            PatenteAcoplado = req.PatenteAcoplado,
            Conductor = req.Conductor,
            Documento = req.Documento,
            Cliente = req.Cliente,
            ClienteId = clienteId,
            Transporte = req.Transporte,
            Producto = req.Producto,
            Procedencia = req.Procedencia,
            Destino = req.Destino,
            Comprobante = req.Comprobante,
            Observaciones = req.Observaciones,
            Operador = operador,
            Balanza = configs.GetValueOrDefault(ConfigKeys.WormsBalanza, "1"),
            PesoEntrada = req.PesoEntrada ?? _scale.Current.Kilogramos,
            TaraPallets = req.TaraPallets,
            EntradaEn = DateTimeOffset.UtcNow
        };
        _db.Pesadas.Add(pesada);
        WormsOutbox.Encolar(_db, configs, pesada, "ENTRADA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    }

    public async Task<ResultadoPesaje> RegistrarSalidaAsync(Guid id, SalidaRequest req)
    {
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == "SALIDA") return ResultadoPesaje.Invalida("Ya tiene salida");

        var configs = await _config.GlobalAsync();
        pesada.PesoSalida = req.PesoSalida ?? _scale.Current.Kilogramos;
        pesada.TaraPallets = req.TaraPallets ?? pesada.TaraPallets;
        pesada.SalidaEn = DateTimeOffset.UtcNow;
        pesada.Estado = "SALIDA";
        var calc = NetoCalculator.Calcular(pesada.PesoEntrada, pesada.PesoSalida.Value, pesada.TaraPallets);
        pesada.Bruto = calc.Bruto;
        pesada.Tara = calc.Tara;
        pesada.Neto = calc.Neto;
        WormsOutbox.Encolar(_db, configs, pesada, "SALIDA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    }

    /// <summary>Por id, o por razón social exacta (sin distinguir mayúsculas) si el operador la escribió a mano.</summary>
    private async Task<Guid?> ResolverCliente(Guid? id, string? razonSocial)
    {
        if (id.HasValue)
        {
            return await _db.Clientes.AnyAsync(c => c.Id == id) ? id : null;
        }
        if (string.IsNullOrWhiteSpace(razonSocial)) return null;

        var buscado = razonSocial.Trim().ToLower();
        var ids = await _db.Clientes.Where(c => c.RazonSocial.ToLower() == buscado).Select(c => c.Id).Take(2).ToListAsync();
        return ids.Count == 1 ? ids[0] : null;
    }
}
