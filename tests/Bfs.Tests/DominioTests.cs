using Bfs.Core;

namespace Bfs.Tests;

public class DominioTests
{
    [Theory]
    [InlineData(30000, 12000, 0, 30000, 12000, 18000)]
    [InlineData(12000, 30000, 0, 30000, 12000, 18000)]
    [InlineData(30000, 12000, 500, 30000, 12000, 17500)]
    [InlineData(10000, 10000, 500, 10000, 10000, 0)]
    [InlineData(30000, 12000, -100, 30000, 12000, 18000)]
    public void Neto_descuenta_tara_y_pallets(int entrada, int salida, int pallets, int bruto, int tara, int neto)
    {
        Assert.Equal((bruto, tara, neto), NetoCalculator.Calcular(entrada, salida, pallets));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("[]")]
    public void Campos_invalidos_vuelven_a_los_defaults(string? json)
    {
        Assert.Equal(FieldCatalog.Defaults, FieldCatalog.Parse(json));
    }

    [Fact]
    public void Campos_por_defecto_se_leen_igual_que_se_guardan()
    {
        Assert.Equal(FieldCatalog.Defaults, FieldCatalog.Parse(FieldCatalog.DefaultJson()));
    }

    [Fact]
    public void El_id_del_nodo_se_conserva_entre_arranques()
    {
        using var tmp = new TempDir();
        var primero = new BfsPaths(tmp.Path).NodoId();
        var segundo = new BfsPaths(tmp.Path).NodoId();
        Assert.NotEqual(Guid.Empty, primero);
        Assert.Equal(primero, segundo);
    }
}
