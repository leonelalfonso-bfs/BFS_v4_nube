using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Bfs.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var env = await CoreWebView2Environment.CreateAsync();
        await Browser.EnsureCoreWebView2Async(env);
        Browser.Source = new Uri("http://127.0.0.1:5080/");
    }
}
