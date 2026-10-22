using System.Security.Cryptography;
using Bfs.Core;
using Bfs.Core.Licencias;

namespace Bfs.Api.Licencias;

public enum EstadoLicencia
{
    Valida,
    Ausente,
    Invalida,
    OtraMaquina,
    Vencida,

    /// <summary>Modo Nube (la suspensión va por tenant) o desarrollo con la licencia desactivada.</summary>
    NoRequerida
}

public record InfoLicencia(EstadoLicencia Estado, string Mensaje, Licencia? Licencia, string Maquina, IReadOnlyList<string> Maquinas)
{
    public bool PuedePesar => Estado is EstadoLicencia.Valida or EstadoLicencia.NoRequerida;
}

/// <summary>
/// Licencia del nodo en <c>license.key</c> dentro de la carpeta de datos. Sin licencia válida no se registran
/// entradas nuevas; se puede consultar todo y cerrar la salida de los camiones que ya entraron.
/// </summary>
public sealed class LicenciaService : IDisposable
{
    private readonly ECDsa _clavePublica;
    private readonly bool _requerida;
    private readonly string _archivo;
    private readonly TimeProvider _clock;
    private readonly Lazy<IReadOnlyList<string>> _maquinas;

    public LicenciaService(BfsOptions options, BfsPaths paths, IConfiguration config, IHostEnvironment env,
        TimeProvider? clock = null, Func<IReadOnlyList<string>>? maquinas = null)
    {
        // Las excepciones solo se aceptan en Development: en una instalación real la clave y el requisito son fijos.
        var dev = env.IsDevelopment();
        var clave = dev ? config["Bfs:Licencia:ClavePublica"] : null;
        _clavePublica = LicenciaCodec.ClaveDesdePem(string.IsNullOrWhiteSpace(clave) ? LicenciaClave.PublicaPem : clave);
        _requerida = options.Modo == ModoDespliegue.Nodo && !(dev && config.GetValue("Bfs:Licencia:Requerida", true) == false);
        _archivo = Path.Combine(paths.Root, "license.key");
        _clock = clock ?? TimeProvider.System;
        _maquinas = new Lazy<IReadOnlyList<string>>(maquinas ?? MaquinaId.Actuales);
    }

    public InfoLicencia Estado()
    {
        if (!_requerida)
        {
            return Info(EstadoLicencia.NoRequerida, "Licencia no requerida en este modo", null);
        }
        if (!File.Exists(_archivo))
        {
            return Info(EstadoLicencia.Ausente, "Sistema sin licencia. Comunicate con BFS e informá el ID de máquina.", null);
        }
        return Evaluar(File.ReadAllText(_archivo));
    }

    /// <summary>Guarda la licencia solo si es válida para esta PC; si no, deja la anterior como estaba.</summary>
    public InfoLicencia Activar(string texto)
    {
        var info = Evaluar(texto);
        if (info.Estado == EstadoLicencia.Valida)
        {
            File.WriteAllText(_archivo, texto.Trim());
        }
        return info;
    }

    private InfoLicencia Evaluar(string texto)
    {
        var licencia = LicenciaCodec.Verificar(texto, _clavePublica);
        if (licencia == null)
        {
            return Info(EstadoLicencia.Invalida, "La licencia no es válida (firma incorrecta o archivo dañado).", null);
        }
        if (!_maquinas.Value.Contains(licencia.Maquina, StringComparer.OrdinalIgnoreCase))
        {
            return Info(EstadoLicencia.OtraMaquina, $"La licencia es para otra PC ({licencia.Maquina}).", licencia);
        }
        if (licencia.Vence is { } vence && _clock.GetUtcNow() > vence)
        {
            return Info(EstadoLicencia.Vencida, $"La licencia venció el {vence.ToLocalTime():dd/MM/yyyy}.", licencia);
        }
        return Info(EstadoLicencia.Valida, $"Licenciado a {licencia.Cliente}", licencia);
    }

    private InfoLicencia Info(EstadoLicencia estado, string mensaje, Licencia? licencia) =>
        new(estado, mensaje, licencia, _maquinas.Value.FirstOrDefault() ?? "ID_NO_DISPONIBLE", _maquinas.Value);

    public void Dispose() => _clavePublica.Dispose();
}
