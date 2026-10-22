using Bfs.Core;

namespace Bfs.Tests;

public class PermisosTests
{
    [Theory]
    [InlineData(0, Roles.Operador)]
    [InlineData(1, Roles.Supervisor)]
    [InlineData(2, Roles.Admin)]
    public void Privilegios_de_la_3x(int privilegio, string rol) => Assert.Equal(rol, Roles.DesdePrivilegio3x(privilegio));

    [Fact]
    public void Cada_rol_suma_permisos_sobre_el_anterior()
    {
        var operador = Permisos.DeRol(Roles.Operador);
        var supervisor = Permisos.DeRol(Roles.Supervisor);
        var admin = Permisos.DeRol(Roles.Admin);
        Assert.Subset(supervisor.ToHashSet(), operador.ToHashSet());
        Assert.Subset(admin.ToHashSet(), supervisor.ToHashSet());
        Assert.DoesNotContain(Permisos.PesajePesoManual, operador);
        Assert.Contains(Permisos.PesajePesoManual, supervisor);
        Assert.DoesNotContain(Permisos.UsuariosGestionar, supervisor);
    }

    [Fact]
    public void Solo_bfs_configura_y_licencia()
    {
        foreach (var rol in Roles.Todos.Where(r => r != Roles.Vendor))
        {
            Assert.False(Permisos.Tiene(rol, Permisos.ConfigBfs), rol);
            Assert.False(Permisos.Tiene(rol, Permisos.LicenciaGestionar), rol);
        }
        Assert.True(Permisos.Tiene(Roles.Vendor, Permisos.ConfigBfs));
    }

    [Fact]
    public void Cliente_solo_ve_sus_pesadas()
    {
        Assert.Equal([Permisos.PesadasVerPropias], Permisos.DeRol(Roles.Cliente));
    }

    [Fact]
    public void Roles_desconocidos_no_tienen_permisos()
    {
        Assert.Empty(Permisos.DeRol("hacker"));
        Assert.Empty(Permisos.DeRol(null));
    }

    [Fact]
    public void El_admin_del_cliente_no_crea_usuarios_bfs()
    {
        Assert.DoesNotContain(Roles.Vendor, Permisos.RolesAsignables(Roles.Admin));
        Assert.Empty(Permisos.RolesAsignables(Roles.Supervisor));
        Assert.Equal(Roles.Todos, Permisos.RolesAsignables(Roles.Vendor));
    }
}
