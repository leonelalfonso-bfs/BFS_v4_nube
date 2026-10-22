namespace Bfs.Core;

public static class NetoCalculator
{
    public static (int Bruto, int Tara, int Neto) Calcular(int pesoEntrada, int pesoSalida, int taraPallets)
    {
        var bruto = Math.Max(pesoEntrada, pesoSalida);
        var tara = Math.Min(pesoEntrada, pesoSalida);
        var neto = Math.Max(0, bruto - tara - Math.Max(0, taraPallets));
        return (bruto, tara, neto);
    }
}
