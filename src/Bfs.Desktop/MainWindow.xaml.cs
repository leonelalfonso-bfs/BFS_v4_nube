using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Bfs.Desktop;

public partial class MainWindow : Window
{
    private readonly string _url;

    public MainWindow(string url)
    {
        _url = url;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Los datos del navegador van con los del sistema, no en la carpeta del programa (que es de solo lectura).
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(programData, "bfs-4", "webview"));
            await Browser.EnsureCoreWebView2Async(env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("Falta el componente Microsoft Edge WebView2. Instalalo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 y volvé a abrir el sistema.",
                "BFS 4.0", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        // Los tickets se abren en una ventana aparte con el visor de PDF de WebView2 (desde ahí se imprimen).
        Browser.CoreWebView2.NewWindowRequested += (_, args) =>
        {
            args.Handled = false;
        };
        Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        Browser.Source = new Uri(_url);
    }
}
