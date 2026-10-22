namespace Bfs.Scale;

/// <summary>
/// El peso está estable cuando no se movió más que la tolerancia durante un tiempo.
/// Se calcula acá y no con la marca del indicador porque cada modelo la manda distinto (o no la manda).
/// </summary>
public sealed class DetectorEstabilidad
{
    private readonly TimeSpan _tiempo;
    private readonly int _toleranciaKg;
    private int _referencia;
    private DateTimeOffset _desde;
    private bool _hayDato;

    public DetectorEstabilidad(TimeSpan? tiempo = null, int toleranciaKg = 0)
    {
        _tiempo = tiempo ?? TimeSpan.FromSeconds(1.5);
        _toleranciaKg = toleranciaKg;
    }

    public bool Registrar(int kg, DateTimeOffset en)
    {
        if (!_hayDato || Math.Abs(kg - _referencia) > _toleranciaKg)
        {
            _referencia = kg;
            _desde = en;
            _hayDato = true;
        }
        return en - _desde >= _tiempo;
    }

    public void Reiniciar() => _hayDato = false;
}
