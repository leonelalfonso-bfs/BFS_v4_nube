using System.Windows;
using Bfs.Api;
using Microsoft.AspNetCore.Builder;

namespace Bfs.Desktop;

public partial class App : Application
{
    // Una sola instancia por PC: dos copias se pelearían por el puerto COM de la balanza y por el puerto web.
    private static readonly Mutex Instancia = new(false, @"Global\BFS-Pesaje-4");
    private WebApplication? _api;
    private bool _esDuena;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            _esDuena = Instancia.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            _esDuena = true;
        }
        if (!_esDuena)
        {
            MessageBox.Show("El Sistema de Pesaje BFS ya está abierto.", "BFS 4.0", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _api = ApiHost.Create(e.Args);
            await _api.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo iniciar el sistema:\n\n{ex.Message}", "BFS 4.0", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var url = _api.Urls.FirstOrDefault() ?? ApiHost.DefaultUrl;
        MainWindow = new MainWindow(url.Replace("0.0.0.0", "127.0.0.1").Replace("[::]", "127.0.0.1"));
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_api != null)
        {
            // Cierra los puertos COM y la base antes de salir.
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _api.StopAsync(limite.Token);
            await _api.DisposeAsync();
        }
        if (_esDuena) Instancia.ReleaseMutex();
        base.OnExit(e);
    }
}
