using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bfs.Core.Licencias;
using Microsoft.Win32;

namespace Bfs.Licencias.Gui;

/// <summary>
/// Generador de licencias con ventana: lo mismo que <c>tools/Bfs.Licencias generar</c>, sin la consola.
/// La clave privada no está en el programa: se lee del archivo de BFS (por defecto %USERPROFILE%\.bfs).
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string CarpetaBfs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bfs");
    private static readonly string ClavePorDefecto = Path.Combine(CarpetaBfs, "bfs-licencias-privada.pem");

    private readonly HistorialLicencias _historial = new(Path.Combine(CarpetaBfs, "licencias-emitidas.csv"));
    private ECDsa? _clave;

    public MainWindow()
    {
        InitializeComponent();
        Vence.SelectedDate = DateTime.Today.AddYears(1);
        Clave.Text = ClavePorDefecto;
        ArchivoHistorial.Text = "Se guarda en " + _historial.Archivo;
        CargarHistorial();
    }

    private void ElegirClave_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Clave privada de licencias de BFS",
            Filter = "Clave privada (*.pem)|*.pem|Todos los archivos|*.*",
            InitialDirectory = Directory.Exists(CarpetaBfs) ? CarpetaBfs : null
        };
        if (dialogo.ShowDialog(this) == true) Clave.Text = dialogo.FileName;
    }

    /// <summary>Carga la clave y avisa si no es la que acepta el sistema (ej. la de la nube o una vieja).</summary>
    private void Clave_TextChanged(object sender, TextChangedEventArgs e)
    {
        _clave?.Dispose();
        _clave = null;
        var ruta = Clave.Text.Trim();
        if (!File.Exists(ruta))
        {
            Estado("No se encuentra el archivo de la clave privada.", ok: false);
            return;
        }
        try
        {
            var clave = LicenciaCodec.ClaveDesdePem(File.ReadAllText(ruta));
            if (!LicenciaCodec.EsLaClaveDelSistema(clave))
            {
                clave.Dispose();
                Estado("Esta clave no es la de licencias de BFS: el sistema rechazaría lo que firme (¿es la de la nube?).", ok: false);
                return;
            }
            _clave = clave;
            Estado("Clave de BFS correcta.", ok: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            Estado("No se pudo leer la clave: " + ex.Message, ok: false);
        }
    }

    private void Estado(string texto, bool ok)
    {
        EstadoClave.Text = (ok ? "✔ " : "✖ ") + texto;
        EstadoClave.Foreground = ok ? new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D)) : new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x41));
        Generar.IsEnabled = ok;
    }

    private void Vencimiento_Changed(object sender, RoutedEventArgs e)
    {
        if (Vence != null) Vence.IsEnabled = SinVencimiento.IsChecked != true;
    }

    private void Generar_Click(object sender, RoutedEventArgs e)
    {
        Error.Text = "";
        var cliente = Cliente.Text.Trim();
        var maquina = MaquinaId.Normalizar(Maquina.Text);
        if (_clave == null) { Error.Text = "Falta la clave privada de BFS."; return; }
        if (cliente.Length == 0) { Error.Text = "Escribí el nombre del cliente."; return; }
        if (maquina == null) { Error.Text = "El ID de máquina tiene que ser MAC- y 12 letras o números (0-9, A-F)."; return; }

        DateOnly? vence = null;
        if (SinVencimiento.IsChecked != true)
        {
            if (Vence.SelectedDate is not { } dia) { Error.Text = "Elegí la fecha de vencimiento."; return; }
            if (dia.Date < DateTime.Today) { Error.Text = "La fecha de vencimiento ya pasó."; return; }
            vence = DateOnly.FromDateTime(dia);
        }

        var (licencia, texto) = LicenciaCodec.Emitir(cliente, maquina, vence, _clave);
        Maquina.Text = maquina;
        Mostrar(licencia, texto);
        _historial.Agregar(licencia, texto);
        CargarHistorial();
    }

    private void Mostrar(Licencia licencia, string texto)
    {
        Texto.Text = texto;
        Resumen.Text = $"{licencia.Cliente} · {licencia.Maquina} · vence {HistorialLicencias.VenceTexto(licencia.Vence)}";
        Copiar.IsEnabled = Guardar.IsEnabled = true;
    }

    private void Copiar_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(Texto.Text);
        Resumen.Text = "Copiada. Pegala en Configuración → Licencia → Activar licencia.";
    }

    private void Guardar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Guardar licencia",
            FileName = "license.key",
            Filter = "Licencia (license.key)|*.key|Todos los archivos|*.*"
        };
        if (dialogo.ShowDialog(this) != true) return;
        File.WriteAllText(dialogo.FileName, Texto.Text);
        Resumen.Text = $"Guardada en {dialogo.FileName}. Va en ProgramData\\bfs-4 (o bfs-4-estandar) de la PC del cliente.";
    }

    private void CargarHistorial()
    {
        Historial.ItemsSource = _historial.Leer().OrderByDescending(x => x.Emitida).ToList();
    }

    private void Historial_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Historial.SelectedItem is not LicenciaEmitida x) return;
        Cliente.Text = x.Cliente;
        Maquina.Text = x.Maquina;
        Texto.Text = x.Texto;
        Resumen.Text = $"{x.Cliente} · {x.Maquina} · vence {x.VenceTexto} (emitida el {x.Emitida:dd/MM/yyyy})";
        Copiar.IsEnabled = Guardar.IsEnabled = x.Texto.Length > 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _clave?.Dispose();
        base.OnClosed(e);
    }
}
