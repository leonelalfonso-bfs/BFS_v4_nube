using System.Security.Cryptography;
using Bfs.Api.Licencias;
using Bfs.Core;
using Bfs.Core.Licencias;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.Internal;

namespace Bfs.Tests;

public class LicenciaTests : IDisposable
{
    private const string Maquina = "MAC-0A1B2C3D4E5F";
    private readonly ECDsa _privada = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly TempDir _tmp = new();
    private readonly Reloj _clock = new(new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero));

    public void Dispose()
    {
        _privada.Dispose();
        _tmp.Dispose();
    }

    /// <summary>Servicio con la clave pública de prueba (solo se acepta en Development) y placas de red fijas.</summary>
    private LicenciaService Servicio(string entorno = "Development", ModoDespliegue modo = ModoDespliegue.Nodo, bool? requerida = null)
    {
        var valores = new Dictionary<string, string?> { ["Bfs:Licencia:ClavePublica"] = _privada.ExportSubjectPublicKeyInfoPem() };
        if (requerida.HasValue) valores["Bfs:Licencia:Requerida"] = requerida.Value.ToString();
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return new LicenciaService(new BfsOptions { Modo = modo }, new BfsPaths(_tmp.Path), config,
            new HostingEnvironment { EnvironmentName = entorno }, _clock, () => ["MAC-FFFFFFFFFFFF", Maquina]);
    }

    private string Emitir(string maquina = Maquina, DateTimeOffset? vence = null) =>
        LicenciaCodec.Firmar(new Licencia("Agro SA", maquina, _clock.Ahora, vence), _privada);

    [Fact]
    public void Firma_valida_se_verifica_y_alterada_no()
    {
        var texto = Emitir();
        using var publica = LicenciaCodec.ClaveDesdePem(_privada.ExportSubjectPublicKeyInfoPem());
        Assert.Equal("Agro SA", LicenciaCodec.Verificar(texto, publica)?.Cliente);

        var otra = LicenciaCodec.Firmar(new Licencia("Agro SA", "MAC-000000000001", _clock.Ahora, null), _privada);
        var mezclada = otra.Split('.')[0] + "." + texto.Split('.')[1];
        Assert.Null(LicenciaCodec.Verificar(mezclada, publica));
        Assert.Null(LicenciaCodec.Verificar("basura", publica));
        Assert.Null(LicenciaCodec.Verificar("", publica));
    }

    [Fact]
    public void Licencia_de_otra_clave_no_vale()
    {
        using var ajena = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var texto = LicenciaCodec.Firmar(new Licencia("Pirata", Maquina, _clock.Ahora, null), ajena);
        Assert.Equal(EstadoLicencia.Invalida, Servicio().Activar(texto).Estado);
    }

    [Fact]
    public void Sin_archivo_esta_ausente_y_no_puede_pesar()
    {
        var info = Servicio().Estado();
        Assert.Equal(EstadoLicencia.Ausente, info.Estado);
        Assert.False(info.PuedePesar);
        Assert.Equal("MAC-FFFFFFFFFFFF", info.Maquina);
    }

    [Fact]
    public void Activar_valida_la_guarda_y_vale_con_cualquier_placa_de_la_pc()
    {
        var servicio = Servicio();
        var info = servicio.Activar(Emitir());
        Assert.Equal(EstadoLicencia.Valida, info.Estado);
        Assert.True(servicio.Estado().PuedePesar);
        Assert.True(File.Exists(_tmp.File("license.key")));
    }

    [Fact]
    public void Licencia_de_otra_pc_no_se_guarda()
    {
        var info = Servicio().Activar(Emitir(maquina: "MAC-123456789ABC"));
        Assert.Equal(EstadoLicencia.OtraMaquina, info.Estado);
        Assert.False(File.Exists(_tmp.File("license.key")));
    }

    [Fact]
    public void Vence_al_pasar_la_fecha()
    {
        var servicio = Servicio();
        servicio.Activar(Emitir(vence: _clock.Ahora.AddDays(15)));
        Assert.True(servicio.Estado().PuedePesar);

        _clock.Ahora = _clock.Ahora.AddDays(15);
        Assert.True(servicio.Estado().PuedePesar, "El último instante todavía vale");

        _clock.Ahora = _clock.Ahora.AddSeconds(1);
        var info = servicio.Estado();
        Assert.Equal(EstadoLicencia.Vencida, info.Estado);
        Assert.False(info.PuedePesar);
    }

    [Fact]
    public void Desactivarla_o_cambiar_la_clave_solo_funciona_en_desarrollo()
    {
        Assert.True(Servicio(requerida: false).Estado().PuedePesar);
        Assert.False(Servicio(entorno: "Production", requerida: false).Estado().PuedePesar);

        // En producción se ignora la clave de prueba: la licencia firmada con ella no vale.
        Assert.Equal(EstadoLicencia.Invalida, Servicio(entorno: "Production").Activar(Emitir()).Estado);
    }

    [Fact]
    public void En_la_nube_no_se_pide_licencia_de_nodo()
    {
        var info = Servicio(entorno: "Production", modo: ModoDespliegue.Nube).Estado();
        Assert.Equal(EstadoLicencia.NoRequerida, info.Estado);
        Assert.True(info.PuedePesar);
    }

    [Fact]
    public void La_clave_publica_embebida_es_valida()
    {
        using var publica = LicenciaCodec.ClaveDesdePem(LicenciaClave.PublicaPem);
        Assert.Equal(256, publica.KeySize);
    }

    [Fact]
    public void Id_de_maquina_tiene_el_formato_de_la_3x()
    {
        Assert.Equal("MAC-708BCD80237C", MaquinaId.Formatear([0x70, 0x8B, 0xCD, 0x80, 0x23, 0x7C]));
        Assert.All(MaquinaId.Actuales(), id => Assert.Matches("^MAC-[0-9A-F]{12}$", id));
    }

    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = ahora;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
