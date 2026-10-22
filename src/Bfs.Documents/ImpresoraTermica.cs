using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Bfs.Documents;

/// <summary>Manda bytes crudos (ESC/POS) a una impresora instalada en la PC de portería.</summary>
public interface IImpresoraTermica
{
    IReadOnlyList<string> Impresoras();

    /// <summary>Lanza si la impresora no existe o Windows rechaza el trabajo.</summary>
    void Imprimir(string impresora, byte[] datos, string documento);
}

/// <summary>En la nube (o fuera de Windows) no hay impresora local: se imprime el PDF desde el navegador.</summary>
public sealed class SinImpresora : IImpresoraTermica
{
    public IReadOnlyList<string> Impresoras() => [];
    public void Imprimir(string impresora, byte[] datos, string documento) =>
        throw new InvalidOperationException("Este equipo no tiene impresoras locales");
}

/// <summary>
/// Spooler de Windows en modo RAW: los bytes llegan tal cual a la impresora, sin driver de por medio
/// (lo mismo que hacía la 3.x con DocFlavor AUTOSENSE).
/// </summary>
public sealed class ImpresoraWindows : IImpresoraTermica
{
    public IReadOnlyList<string> Impresoras()
    {
        if (!OperatingSystem.IsWindows()) return [];
        const int local = 0x2, conexiones = 0x4;
        EnumPrinters(local | conexiones, null, 4, IntPtr.Zero, 0, out var necesarios, out _);
        if (necesarios == 0) return [];

        var buffer = Marshal.AllocHGlobal(necesarios);
        try
        {
            if (!EnumPrinters(local | conexiones, null, 4, buffer, necesarios, out _, out var cantidad)) return [];
            var tamano = Marshal.SizeOf<PrinterInfo4>();
            var nombres = new List<string>();
            for (var i = 0; i < cantidad; i++)
            {
                var info = Marshal.PtrToStructure<PrinterInfo4>(buffer + i * tamano);
                if (info.PrinterName != null) nombres.Add(info.PrinterName);
            }
            return nombres.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Imprimir(string impresora, byte[] datos, string documento)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("La impresión térmica necesita Windows");
        if (!OpenPrinter(impresora, out var handle, IntPtr.Zero)) throw Error($"No se encontró la impresora \"{impresora}\"");
        try
        {
            var doc = new DocInfo1 { DocName = documento, DataType = "RAW" };
            if (StartDocPrinter(handle, 1, ref doc) == 0) throw Error("La impresora rechazó el trabajo");
            try
            {
                if (!StartPagePrinter(handle)) throw Error("La impresora rechazó el trabajo");
                var ptr = Marshal.AllocHGlobal(datos.Length);
                try
                {
                    Marshal.Copy(datos, 0, ptr, datos.Length);
                    if (!WritePrinter(handle, ptr, datos.Length, out var escritos) || escritos != datos.Length)
                    {
                        throw Error("No se pudo enviar el ticket a la impresora");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                    EndPagePrinter(handle);
                }
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    private static InvalidOperationException Error(string mensaje) =>
        new($"{mensaje} ({new Win32Exception(Marshal.GetLastWin32Error()).Message})");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo1
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PrinterInfo4
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? PrinterName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "StartDocPrinterW")]
    private static extern int StartDocPrinter(IntPtr handle, int level, ref DocInfo1 doc);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, IntPtr bytes, int count, out int written);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "EnumPrintersW")]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr buffer, int size, out int needed, out int returned);
}
