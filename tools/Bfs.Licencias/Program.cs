using System.Globalization;
using System.Security.Cryptography;
using Bfs.Core.Licencias;

// Herramienta de BFS para emitir licencias de nodos. La clave privada NUNCA va al repositorio.
//
//   nueva-clave --privada <archivo.pem>
//   generar     --privada <archivo.pem> --cliente "Razón social" --maquina MAC-XXXXXXXXXXXX [--vence aaaa-mm-dd] [--salida license.key]
//   ver         <license.key>

Console.OutputEncoding = System.Text.Encoding.UTF8;
var opts = ParseArgs(args.Skip(1).ToArray());
switch (args.FirstOrDefault())
{
    case "nueva-clave":
        return NuevaClave(Requerido(opts, "privada"));
    case "generar":
        return Generar(opts);
    case "ver":
        return Ver(args.ElementAtOrDefault(1));
    default:
        Console.WriteLine("""
            Uso:
              bfs-licencias nueva-clave --privada <archivo.pem>
              bfs-licencias generar --privada <archivo.pem> --cliente "Razón social" --maquina MAC-XXXXXXXXXXXX [--vence aaaa-mm-dd] [--salida license.key]
              bfs-licencias ver <license.key>
            """);
        return 1;
}

static int NuevaClave(string privada)
{
    if (File.Exists(privada))
    {
        Console.Error.WriteLine($"Ya existe {privada}. No se sobreescribe: perderías la clave con la que firmaste las licencias emitidas.");
        return 2;
    }

    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(privada))!);
    File.WriteAllText(privada, ecdsa.ExportPkcs8PrivateKeyPem());
    Console.WriteLine($"Clave privada guardada en {privada}. Hacé una copia de seguridad fuera de esta PC.");
    Console.WriteLine();
    Console.WriteLine("Clave pública (va en src/Bfs.Core/Licencias/LicenciaClave.cs):");
    Console.WriteLine(ecdsa.ExportSubjectPublicKeyInfoPem());
    return 0;
}

static int Generar(Dictionary<string, string> opts)
{
    using var clave = LicenciaCodec.ClaveDesdePem(File.ReadAllText(Requerido(opts, "privada")));
    var maquina = Requerido(opts, "maquina").Trim().ToUpperInvariant();
    if (!maquina.StartsWith("MAC-", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("La máquina tiene que tener el formato MAC-XXXXXXXXXXXX (se ve en el sistema, pantalla Licencia).");
        return 2;
    }

    // Vale hasta el último segundo de ese día, en la hora de esta PC (Argentina).
    DateTimeOffset? vence = null;
    if (opts.TryGetValue("vence", out var v))
    {
        var dia = DateTime.ParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fin = dia.AddDays(1).AddSeconds(-1);
        vence = new DateTimeOffset(fin, TimeZoneInfo.Local.GetUtcOffset(fin));
    }

    var licencia = new Licencia(Requerido(opts, "cliente"), maquina, DateTimeOffset.UtcNow, vence);
    var texto = LicenciaCodec.Firmar(licencia, clave);

    if (opts.TryGetValue("salida", out var salida))
    {
        File.WriteAllText(salida, texto);
        Console.WriteLine($"Licencia guardada en {salida}");
    }
    else
    {
        Console.WriteLine(texto);
    }
    Console.Error.WriteLine($"Cliente: {licencia.Cliente} · Máquina: {licencia.Maquina} · Vence: {(licencia.Vence?.ToLocalTime().ToString("yyyy-MM-dd") ?? "nunca")}");
    return 0;
}

static int Ver(string? archivo)
{
    if (archivo == null || !File.Exists(archivo))
    {
        Console.Error.WriteLine("Indicá el archivo de licencia.");
        return 2;
    }

    using var publica = LicenciaCodec.ClaveDesdePem(LicenciaClave.PublicaPem);
    var licencia = LicenciaCodec.Verificar(File.ReadAllText(archivo), publica);
    if (licencia == null)
    {
        Console.Error.WriteLine("Firma inválida: la licencia fue modificada o no la emitió esta clave de BFS.");
        return 3;
    }
    Console.WriteLine($"Cliente: {licencia.Cliente}\nMáquina: {licencia.Maquina}\nEmitida: {licencia.Emitida:yyyy-MM-dd HH:mm} UTC\nVence: {(licencia.Vence?.ToLocalTime().ToString("yyyy-MM-dd") ?? "nunca")}");
    return 0;
}

static Dictionary<string, string> ParseArgs(string[] a)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < a.Length - 1; i++)
    {
        if (a[i].StartsWith("--", StringComparison.Ordinal)) d[a[i][2..]] = a[++i];
    }
    return d;
}

static string Requerido(Dictionary<string, string> opts, string nombre) =>
    opts.TryGetValue(nombre, out var v) && !string.IsNullOrWhiteSpace(v)
        ? v
        : throw new ArgumentException($"Falta --{nombre}");
