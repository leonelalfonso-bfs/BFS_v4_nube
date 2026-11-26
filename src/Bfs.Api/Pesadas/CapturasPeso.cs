using System.Collections.Concurrent;
using System.Text.Json;

namespace Bfs.Api.Pesadas;

/// <summary>Un peso tomado de la balanza en un momento, para guardar la pesada después de cargar los datos.</summary>
public record CapturaPeso(Guid Id, int Kilogramos, string? Balanza, DateTimeOffset En, Guid? BalanzaId = null)
{
    /// <summary>Fotos de las cámaras sacadas en el momento de la captura (automatización). No van a la pantalla.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<Automatizacion.FotoCamara> Fotos { get; init; } = [];

    /// <summary>Cuántas fotos se sacaron (para mostrarlo en pantalla).</summary>
    public int CantidadFotos => Fotos.Count;
}

/// <summary>
/// "Capturar peso, completar los datos y aceptar": con muchos camiones, el operador congela el peso apenas el camión
/// está quieto (aunque enseguida se retire de la balanza) y carga los datos sin apuro. Persiste en disco del puesto
/// si se indica ruta, vence a las <see cref="Vigencia"/> y se usa una sola vez: recién cuando la pesada se guardó bien.
/// </summary>
public sealed class CapturasPeso
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TimeProvider _reloj;
    private readonly string? _archivo;
    private readonly Lock _disco = new();
    private readonly ConcurrentDictionary<Guid, CapturaPeso> _capturas = new();

    public CapturasPeso(TimeProvider? reloj = null, string? archivo = null)
    {
        _reloj = reloj ?? TimeProvider.System;
        _archivo = archivo;
        CargarDeDisco();
    }

    public CapturaPeso Guardar(int kilogramos, string? balanza, IReadOnlyList<Automatizacion.FotoCamara>? fotos = null, Guid? balanzaId = null)
    {
        Limpiar();
        var c = new CapturaPeso(Guid.NewGuid(), kilogramos, balanza, _reloj.GetUtcNow(), balanzaId) { Fotos = fotos ?? [] };
        _capturas[c.Id] = c;
        PersistirEnDisco();
        return c;
    }

    /// <summary>La captura si existe y no venció. No la consume: ver <see cref="Usar"/>.</summary>
    public CapturaPeso? Buscar(Guid id) =>
        _capturas.TryGetValue(id, out var c) && _reloj.GetUtcNow() - c.En <= Vigencia ? c : null;

    /// <summary>Ya se guardó la pesada con este peso: no se puede volver a usar.</summary>
    public void Usar(Guid id)
    {
        if (_capturas.TryRemove(id, out _)) PersistirEnDisco();
    }

    private void Limpiar()
    {
        var limite = _reloj.GetUtcNow() - Vigencia;
        foreach (var (id, c) in _capturas)
        {
            if (c.En < limite) _capturas.TryRemove(id, out _);
        }
    }

    private void CargarDeDisco()
    {
        if (string.IsNullOrWhiteSpace(_archivo) || !File.Exists(_archivo)) return;
        try
        {
            var lista = JsonSerializer.Deserialize<List<CapturaPeso>>(File.ReadAllText(_archivo), Json) ?? [];
            var limite = _reloj.GetUtcNow() - Vigencia;
            foreach (var c in lista.Where(x => x.Id != Guid.Empty && x.En >= limite))
            {
                _capturas[c.Id] = c;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
    }

    private void PersistirEnDisco()
    {
        if (string.IsNullOrWhiteSpace(_archivo)) return;
        lock (_disco)
        {
            try
            {
                Limpiar();
                var dir = Path.GetDirectoryName(_archivo);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tmp = _archivo + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_capturas.Values.ToList(), Json));
                File.Move(tmp, _archivo, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
