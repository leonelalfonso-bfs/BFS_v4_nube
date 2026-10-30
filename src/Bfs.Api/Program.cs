using Bfs.Api;

// "dotnet Bfs.Api.dll --salud": chequeo de salud del contenedor (la imagen de .NET no trae curl).
if (args.Contains("--salud"))
{
    var url = (Environment.GetEnvironmentVariable("Urls") ?? ApiHost.DefaultUrl).Split(';')[0].Replace("0.0.0.0", "127.0.0.1").Replace("+", "127.0.0.1");
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        return (await http.GetAsync(url.TrimEnd('/') + "/api/health")).IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return 1;
    }
}

var app = ApiHost.Create(args);
app.Run();
return 0;
