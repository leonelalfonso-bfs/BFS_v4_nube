using System.Text.Json;

namespace Bfs.Core;

/// <summary>Qué hay conectado a la placa de entradas / salidas (Modbus TCP).</summary>
public static class TiposDispositivo
{
    /// <summary>Salida de la placa. Encendida = verde.</summary>
    public const string Semaforo = "semaforo";

    /// <summary>Salida de la placa. Encendida = abierta.</summary>
    public const string Barrera = "barrera";

    /// <summary>Entrada de la placa (barrera infrarroja en una punta de la balanza). Prendida = en verde, nadie la corta; apagada = cortada, un camión está pasando por esa punta.</summary>
    public const string Sensor = "sensor";

    public static bool EsSalida(string tipo) => tipo is Semaforo or Barrera;
}

public static class LadosBalanza
{
    public const string Entrada = "entrada";
    public const string Salida = "salida";
}

/// <param name="Punto">Número de entrada (sensores) o de salida (semáforos y barreras) de la placa, del 1 al 8.</param>
/// <param name="Balanza">1 = la primera balanza (por orden), 2 = la segunda.</param>
/// <param name="Lado">Punta de la balanza: entrada o salida del camión.</param>
public record DispositivoAutomatizacion(string Tipo, string Nombre, int Punto, int Balanza, string Lado, bool Instalado = true);

/// <param name="Url">Dirección de la foto de la cámara (ej. Dahua: http://IP/cgi-bin/snapshot.cgi). "sim" = cámara simulada.</param>
public record CamaraAutomatizacion(string Nombre, string Url, string? Usuario = null, string? Clave = null, bool Habilitada = true);

/// <summary>
/// Automatización de la 3.0 "Automatización": semáforos, barreras y sensores por Modbus TCP, y cámaras IP.
/// La habilita BFS (Configuración → Automatización) en cualquier edición. Apagada no corre nada.
/// </summary>
public class ConfigAutomatizacion
{
    public const int PuntosPlaca = 8;

    public bool Habilitada { get; set; }

    /// <summary>Placa simulada (para probar sin hardware): las entradas se prenden y apagan desde la pantalla de prueba.</summary>
    public bool Simulada { get; set; }

    public string ModbusIp { get; set; } = "192.168.1.200";
    public int ModbusPuerto { get; set; } = 502;
    public int ModbusUnidad { get; set; } = 1;

    public List<DispositivoAutomatizacion> Dispositivos { get; set; } = Predeterminados();

    /// <summary>
    /// Al aceptar una pesada: semáforo de salida en verde y barrera de salida abierta. Cuando el camión sale (un sensor de esa balanza
    /// se corta y todos vuelven a verde): barreras cerradas, semáforo de salida en rojo y el de entrada en verde.
    /// </summary>
    public bool SecuenciaAutomatica { get; set; }

    /// <summary>Saca fotos con las cámaras al capturar el peso (o al aceptar, si no se capturó) y van en el ticket.</summary>
    public bool CapturarFotos { get; set; } = true;

    public List<CamaraAutomatizacion> Camaras { get; set; } = [];

    /// <summary>Como la 3.x: semáforos en las salidas 1-4, barreras en las 5-8 y sensores en las entradas 1-4.</summary>
    public static List<DispositivoAutomatizacion> Predeterminados() =>
    [
        new(TiposDispositivo.Semaforo, "Semáforo entrada B1", 1, 1, LadosBalanza.Entrada),
        new(TiposDispositivo.Semaforo, "Semáforo salida B1", 2, 1, LadosBalanza.Salida),
        new(TiposDispositivo.Semaforo, "Semáforo entrada B2", 3, 2, LadosBalanza.Entrada, false),
        new(TiposDispositivo.Semaforo, "Semáforo salida B2", 4, 2, LadosBalanza.Salida, false),
        new(TiposDispositivo.Barrera, "Barrera entrada B1", 5, 1, LadosBalanza.Entrada),
        new(TiposDispositivo.Barrera, "Barrera salida B1", 6, 1, LadosBalanza.Salida),
        new(TiposDispositivo.Barrera, "Barrera entrada B2", 7, 2, LadosBalanza.Entrada, false),
        new(TiposDispositivo.Barrera, "Barrera salida B2", 8, 2, LadosBalanza.Salida, false),
        new(TiposDispositivo.Sensor, "Sensor entrada B1", 1, 1, LadosBalanza.Entrada),
        new(TiposDispositivo.Sensor, "Sensor salida B1", 2, 1, LadosBalanza.Salida),
        new(TiposDispositivo.Sensor, "Sensor entrada B2", 3, 2, LadosBalanza.Entrada, false),
        new(TiposDispositivo.Sensor, "Sensor salida B2", 4, 2, LadosBalanza.Salida, false)
    ];

    public IEnumerable<DispositivoAutomatizacion> Instalados(string tipo, int? balanza = null) =>
        Dispositivos.Where(d => d.Instalado && d.Tipo == tipo && (balanza == null || d.Balanza == balanza));

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ConfigAutomatizacion Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ConfigAutomatizacion();
        try
        {
            return JsonSerializer.Deserialize<ConfigAutomatizacion>(json, Json) ?? new ConfigAutomatizacion();
        }
        catch (JsonException)
        {
            return new ConfigAutomatizacion();
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Lo que está mal cargado, o null.</summary>
    public string? Validar()
    {
        if (ModbusPuerto is < 1 or > 65535) return "Puerto Modbus inválido";
        if (ModbusUnidad is < 0 or > 255) return "Unidad Modbus inválida";
        if (Dispositivos.FirstOrDefault(d => d.Punto is < 1 or > PuntosPlaca) is { } malo) return $"{malo.Nombre}: el punto va del 1 al {PuntosPlaca}";
        if (Dispositivos.FirstOrDefault(d => d.Balanza is < 1 or > 2) is { } malaBal) return $"{malaBal.Nombre}: la balanza es 1 o 2";
        // Dos semáforos o barreras en la misma salida se pisarían.
        var repetida = Dispositivos.Where(d => d.Instalado && TiposDispositivo.EsSalida(d.Tipo)).GroupBy(d => d.Punto).FirstOrDefault(g => g.Count() > 1);
        if (repetida != null) return $"La salida {repetida.Key} está asignada dos veces";
        if (Camaras.Count > 2) return "Hasta dos cámaras";
        if (Camaras.FirstOrDefault(c => string.IsNullOrWhiteSpace(c.Url)) is { } sinUrl) return $"{sinUrl.Nombre}: falta la dirección";
        return null;
    }
}

/// <summary>Una foto de una cámara guardada con la pesada (en la carpeta de datos del puesto).</summary>
/// <param name="Momento">entrada o salida.</param>
/// <param name="Archivo">Ruta relativa a la carpeta de datos (fotos/aaaa/mm/...jpg).</param>
public record FotoPesada(string Momento, string Camara, string Archivo);
