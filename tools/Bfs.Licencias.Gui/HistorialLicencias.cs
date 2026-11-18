using System.Globalization;
using System.IO;
using System.Text;
using Bfs.Core.Licencias;

namespace Bfs.Licencias.Gui;

public record LicenciaEmitida(DateTime Emitida, string Cliente, string Maquina, string VenceTexto, string Texto);

/// <summary>
/// Registro de las licencias emitidas en un CSV (se abre con Excel). Sirve para volver a darle la licencia a un cliente
/// sin generarla de nuevo y para saber qué se entregó a quién.
/// </summary>
public sealed class HistorialLicencias(string archivo)
{
    private const string Encabezado = "Emitida;Cliente;Maquina;Vence;Licencia";

    public string Archivo { get; } = archivo;

    public static string VenceTexto(DateTimeOffset? vence) => vence?.ToLocalTime().ToString("dd/MM/yyyy") ?? "nunca";

    public void Agregar(Licencia licencia, string texto)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Archivo)!);
        var nuevo = !File.Exists(Archivo);
        using var w = new StreamWriter(Archivo, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        if (nuevo) w.WriteLine(Encabezado);
        w.WriteLine(string.Join(';',
            licencia.Emitida.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            Campo(licencia.Cliente), licencia.Maquina, VenceTexto(licencia.Vence), texto));
    }

    public IReadOnlyList<LicenciaEmitida> Leer()
    {
        if (!File.Exists(Archivo)) return [];
        var lista = new List<LicenciaEmitida>();
        foreach (var linea in File.ReadLines(Archivo).Skip(1))
        {
            var c = Separar(linea);
            if (c.Count < 5 || !DateTime.TryParseExact(c[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var emitida)) continue;
            lista.Add(new LicenciaEmitida(emitida, c[1], c[2], c[3], c[4]));
        }
        return lista;
    }

    /// <summary>Entre comillas si tiene ; o comillas (razones sociales como "Pérez; Hijos").</summary>
    private static string Campo(string s) => s.Contains(';') || s.Contains('"') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static List<string> Separar(string linea)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        var comillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var ch = linea[i];
            if (comillas)
            {
                if (ch == '"' && i + 1 < linea.Length && linea[i + 1] == '"') { actual.Append('"'); i++; }
                else if (ch == '"') comillas = false;
                else actual.Append(ch);
            }
            else if (ch == '"') comillas = true;
            else if (ch == ';') { campos.Add(actual.ToString()); actual.Clear(); }
            else actual.Append(ch);
        }
        campos.Add(actual.ToString());
        return campos;
    }
}
