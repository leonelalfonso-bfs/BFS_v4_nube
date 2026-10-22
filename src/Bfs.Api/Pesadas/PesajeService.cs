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
    private readonly IBalanzas _balanzas;
    private readonly ConfigService _config;
    private readonly LicenciaService _licencia;

    public PesajeService(BfsDbContext db, IBalanzas balanzas, ConfigService config, LicenciaService licencia)
    {
        _db = db;
        _balanzas = balanzas;
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

        var lectura = _balanzas.Lectura(req.BalanzaId);
        if (req.PesoEntrada == null && LecturaInvalida(lectura) is { } errorBalanza) return ResultadoPesaje.Invalida(errorBalanza);

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
            PatenteChasis = Patente.Normalizar(req.PatenteChasis),
            PatenteAcoplado = string.IsNullOrWhiteSpace(req.PatenteAcoplado) ? null : Patente.Normalizar(req.PatenteAcoplado),
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
            Balanza = string.IsNullOrEmpty(lectura.Nombre) ? null : lectura.Nombre,
            PesoEntrada = req.PesoEntrada ?? lectura.Kilogramos,
            TaraPallets = req.TaraPallets,
            EntradaEn = DateTimeOffset.UtcNow
        };
        if (string.IsNullOrEmpty(pesada.PatenteChasis)) return ResultadoPesaje.Invalida("Falta la patente");
        _db.Pesadas.Add(pesada);
        await RecordarVehiculo(pesada);
        WormsOutbox.Encolar(_db, configs, pesada, "ENTRADA");
        await _db.SaveChangesAsync();
        return ResultadoPesaje.Ok(pesada);
    }

    public async Task<ResultadoPesaje> RegistrarSalidaAsync(Guid id, SalidaRequest req)
    {
        var pesada = await _db.Pesadas.FirstOrDefaultAsync(p => p.Id == id);
        if (pesada == null) return ResultadoPesaje.Inexistente();
        if (pesada.Estado == "SALIDA") return ResultadoPesaje.Invalida("Ya tiene salida");

        var lectura = _balanzas.Lectura(req.BalanzaId);
        if (req.PesoSalida == null && LecturaInvalida(lectura) is { } errorBalanza) return ResultadoPesaje.Invalida(errorBalanza);

        var configs = await _config.GlobalAsync();
        pesada.PesoSalida = req.PesoSalida ?? lectura.Kilogramos;
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

    /// <summary>No se toma el peso de una balanza que no está leyendo: guardaría el último valor o cero.</summary>
    private static string? LecturaInvalida(LecturaBalanza l) => l.Estado switch
    {
        "abierta" or "simulada" => null,
        "sin balanza" => "No hay ninguna balanza configurada",
        _ => $"La balanza {l.Nombre} no está leyendo ({l.Estado}{(l.Error is null ? "" : ": " + l.Error)})"
    };

    /// <summary>Guarda los datos de esta patente para autocompletar la próxima vez. Lo vacío no pisa lo que había.</summary>
    private async Task RecordarVehiculo(Pesada p)
    {
        var datos = await _db.DatosVehiculos.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.PatenteChasis == p.PatenteChasis);
        if (datos == null)
        {
            datos = new DatosVehiculo { Id = Guid.NewGuid(), PatenteChasis = p.PatenteChasis };
            _db.DatosVehiculos.Add(datos);
        }
        datos.Eliminado = false;
        datos.PatenteAcoplado = p.PatenteAcoplado ?? datos.PatenteAcoplado;
        datos.Conductor = Vacio(p.Conductor) ?? datos.Conductor;
        datos.Documento = Vacio(p.Documento) ?? datos.Documento;
        datos.Transporte = Vacio(p.Transporte) ?? datos.Transporte;
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
