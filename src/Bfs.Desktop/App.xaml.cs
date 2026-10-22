using System.Windows;
using Bfs.Api;
using Microsoft.AspNetCore.Builder;

namespace Bfs.Desktop;

public partial class App : Application
{
    private WebApplication? _api;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _api = ApiHost.Create([]);
        _ = _api.StartAsync();
        await Task.Delay(400);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_api != null)
        {
            await _api.StopAsync();
        }
        base.OnExit(e);
    }
}
