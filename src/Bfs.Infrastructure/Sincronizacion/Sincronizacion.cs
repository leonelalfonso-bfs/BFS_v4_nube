using System.Text.Json;
using Bfs.Core.Entities;
using Bfs.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Bfs.Infrastructure.Sincronizacion;

/// <summary>Qué viaja entre la portería y la nube, y en qué orden (primero lo que otros referencian).</summary>
public static class TiposSync
{
    public const string Empresas = "empresas";
    public const string Clientes = "clientes";
    public const string Transportes = "transportes";
    public const string Productos = "productos";
    public const string Exportadores = "exportadores";
    public const string Taras = "taras";
    public const string Vehiculos = "vehiculos";
    public const string Pesadas = "pesadas";
    public const string Usuarios = "usuarios";

    /// <summary>De la portería a la nube.</summary>
    public static IReadOnlyList<string> Subida { get; } = [Empresas, Clientes, Transportes, Productos, Exportadores, Taras, Vehiculos, Pesadas];

    /// <summary>
    /// De la nube a la portería: lo mismo más los usuarios, que se administran en la nube y tienen que poder
    /// entrar en la portería sin internet. Los usuarios de la portería no suben (ahí están los de prueba con clave conocida).
    /// </summary>
    public static IReadOnlyList<string> Bajada { get; } = [.. Subida, Usuarios];

    public static IQueryable<EntidadSync> Consulta(BfsDbContext db, string tipo) => tipo switch
    {
        Empresas => db.Empresas.IgnoreQueryFilters(),
        Clientes => db.Clientes.IgnoreQueryFilters(),
        Transportes => db.Transportes.IgnoreQueryFilters(),
        Productos => db.Productos.IgnoreQueryFilters(),
        Exportadores => db.Exportadores.IgnoreQueryFilters(),
        Taras => db.Taras.IgnoreQueryFilters(),
        Vehiculos => db.DatosVehiculos.IgnoreQueryFilters(),
        Pesadas => db.Pesadas.IgnoreQueryFilters(),
        Usuarios => db.Usuarios.IgnoreQueryFilters(),
        _ => throw new ArgumentException($"Tipo de sincronización desconocido: {tipo}")
    };

    public static Type Clr(string tipo) => tipo switch
    {
        Empresas => typeof(Empresa),
        Clientes => typeof(Cliente),
        Transportes => typeof(Transporte),
        Productos => typeof(Producto),
        Exportadores => typeof(Exportador),
        Taras => typeof(TaraConocida),
        Vehiculos => typeof(DatosVehiculo),
        Pesadas => typeof(Pesada),
        Usuarios => typeof(Usuario),
        _ => throw new ArgumentException($"Tipo de sincronización desconocido: {tipo}")
    };
}

public record ResultadoAplicar(int Aplicados, int Iguales, IReadOnlyList<string> Conflictos);

/// <summary>
/// Aplica filas que llegan de la otra punta. Por Id: si no existe se crea; si existe, gana la modificación más nueva
/// (y a igual fecha, la de mayor versión). Si choca con otro registro por un dato único (ej. el mismo producto
/// cargado en los dos lados), no se aplica y se informa como conflicto.
/// </summary>
public static class AplicadorSync
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <param name="esNube">En la nube, una empresa de la portería reemplaza a la vacía que se creó con el alta si tienen el mismo nombre.</param>
    public static async Task<ResultadoAplicar> AplicarAsync(BfsDbContext db, string tipo, IEnumerable<JsonElement> filas, bool esNube)
    {
        int aplicados = 0, iguales = 0;
        var conflictos = new List<string>();
        db.AplicandoSincronizacion = true;
        try
        {
            foreach (var fila in filas)
            {
                if (fila.Deserialize(TiposSync.Clr(tipo), Json) is not EntidadSync entrante || entrante.Id == Guid.Empty)
                {
                    conflictos.Add($"{tipo}: fila ilegible");
                    continue;
                }

                var actual = await TiposSync.Consulta(db, tipo).FirstOrDefaultAsync(x => x.Id == entrante.Id);
                if (actual != null && !EsMasNueva(entrante, actual))
                {
                    iguales++;
                    continue;
                }

                try
                {
                    if (actual == null)
                    {
                        if (esNube && entrante is Empresa empresa) await LiberarNombreDeEmpresa(db, empresa);
                        db.Add(entrante);
                    }
                    else
                    {
                        db.Entry(actual).CurrentValues.SetValues(entrante);
                        if (actual is Pesada pesada) pesada.Aduana = ((Pesada)entrante).Aduana;
                    }
                    await db.SaveChangesAsync();
                    aplicados++;
                }
                catch (DbUpdateException ex)
                {
                    db.ChangeTracker.Clear();
                    conflictos.Add($"{tipo} {Describir(entrante)}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }
        finally
        {
            db.AplicandoSincronizacion = false;
            db.ChangeTracker.Clear();
        }
        return new ResultadoAplicar(aplicados, iguales, conflictos);
    }

    public static bool EsMasNueva(EntidadSync entrante, EntidadSync actual) =>
        entrante.ModificadoEn > actual.ModificadoEn || (entrante.ModificadoEn == actual.ModificadoEn && entrante.Version > actual.Version);

    /// <summary>
    /// El alta de un cliente en la nube crea una empresa con su nombre. Cuando la portería sube la suya (con sus pesadas),
    /// la de la nube, si está sin usar, se aparta para que no choquen los nombres.
    /// </summary>
    private static async Task LiberarNombreDeEmpresa(BfsDbContext db, Empresa entrante)
    {
        var nombre = entrante.Nombre.ToLower();
        var otra = await db.Empresas.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id != entrante.Id && e.Nombre.ToLower() == nombre);
        if (otra == null || await db.Pesadas.IgnoreQueryFilters().AnyAsync(p => p.EmpresaId == otra.Id)) return;
        otra.Nombre = $"{otra.Nombre} (anterior {otra.Id.ToString()[..8]})";
        otra.Eliminado = true;
        otra.ModificadoEn = DateTimeOffset.UtcNow;
        otra.Version++;
        await db.SaveChangesAsync();
    }

    private static string Describir(EntidadSync e) => e switch
    {
        Pesada p => $"ticket {p.TicketNumero}",
        Empresa x => x.Nombre,
        Cliente x => x.RazonSocial,
        Transporte x => x.RazonSocial,
        Producto x => x.Nombre,
        Exportador x => x.RazonSocial,
        TaraConocida x => x.Identificador,
        DatosVehiculo x => x.PatenteChasis,
        Usuario x => x.Email,
        _ => e.Id.ToString()
    };
}
