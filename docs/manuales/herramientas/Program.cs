// Saca capturas de la app para los manuales manejando Edge sin ventana por el protocolo de depuración (CDP).
//   dotnet run -- <url de la app> <carpeta de salida> <archivo de pasos .json>
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var app = args[0];
var salida = args[1];
var pasos = JsonNode.Parse(File.ReadAllText(args[2]))!.AsArray();
Directory.CreateDirectory(salida);

var perfil = Path.Combine(Path.GetTempPath(), "bfs-capturas-edge");
if (Directory.Exists(perfil)) Directory.Delete(perfil, true);
var edge = Process.Start(new ProcessStartInfo(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    $"--headless=new --remote-debugging-port=9333 --user-data-dir=\"{perfil}\" --no-first-run --hide-scrollbars --window-size=1366,860 about:blank"))!;
try
{
    using var http = new HttpClient();
    string? ws = null;
    for (var i = 0; i < 50 && ws == null; i++)
    {
        try
        {
            var lista = JsonNode.Parse(await http.GetStringAsync("http://127.0.0.1:9333/json/list"))!.AsArray();
            ws = lista.FirstOrDefault(x => x!["type"]!.GetValue<string>() == "page")?["webSocketDebuggerUrl"]?.GetValue<string>();
        }
        catch (HttpRequestException) { }
        if (ws == null) await Task.Delay(200);
    }
    using var cdp = new ClientWebSocket();
    await cdp.ConnectAsync(new Uri(ws!), CancellationToken.None);
    var id = 0;

    async Task<JsonNode?> Enviar(string metodo, object? parametros = null)
    {
        var miId = ++id;
        var json = JsonSerializer.Serialize(new { id = miId, method = metodo, @params = parametros ?? new { } });
        await cdp.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
        var buffer = new byte[1 << 20];
        while (true)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult r;
            do
            {
                r = await cdp.ReceiveAsync(buffer, CancellationToken.None);
                ms.Write(buffer, 0, r.Count);
            } while (!r.EndOfMessage);
            var msg = JsonNode.Parse(ms.ToArray())!;
            if (msg["id"]?.GetValue<int>() != miId) continue;
            if (msg["error"] != null) throw new Exception($"{metodo}: {msg["error"]}");
            return msg["result"];
        }
    }

    async Task<JsonNode?> Js(string codigo)
    {
        var r = await Enviar("Runtime.evaluate", new { expression = $"(async () => {{ {codigo} }})()", awaitPromise = true, returnByValue = true });
        if (r?["exceptionDetails"] != null) throw new Exception("JS: " + r["exceptionDetails"]!.ToJsonString());
        return r?["result"]?["value"];
    }

    await Enviar("Emulation.setDeviceMetricsOverride", new { width = 1366, height = 860, deviceScaleFactor = 1, mobile = false });
    await Enviar("Page.enable");
    await Enviar("Page.navigate", new { url = app });
    await Task.Delay(2500);

    foreach (var paso in pasos)
    {
        var nombre = paso!["nombre"]!.GetValue<string>();
        if (paso["js"] is { } js) await Js(js.GetValue<string>());
        await Task.Delay(paso["espera"]?.GetValue<int>() ?? 1200);
        if (paso["sinCaptura"]?.GetValue<bool>() == true) { Console.WriteLine($"  {nombre} (sin captura)"); continue; }

        object parametros = new { format = "png" };
        if (paso["recorte"] is { } sel)
        {
            // Recorte de un elemento (o del rango entre dos: "desde|hasta"), con un margen.
            var partes = sel.GetValue<string>().Split('|');
            var r = await Js($$"""
                const a = document.querySelector({{JsonSerializer.Serialize(partes[0])}});
                const b = document.querySelector({{JsonSerializer.Serialize(partes.Length > 1 ? partes[1] : partes[0])}});
                if (!a || !b) return null;
                const ra = a.getBoundingClientRect(), rb = b.getBoundingClientRect();
                const x = Math.min(ra.left, rb.left) + window.scrollX, y = ra.top + window.scrollY;
                return { x: Math.max(0, x - 12), y: Math.max(0, y - 12),
                         w: Math.max(ra.right, rb.right) - Math.min(ra.left, rb.left) + 24, h: rb.bottom - ra.top + 24 };
                """);
            if (r == null) throw new Exception($"{nombre}: no se encontró {sel}");
            parametros = new
            {
                format = "png", captureBeyondViewport = true,
                clip = new { x = r["x"]!.GetValue<double>(), y = r["y"]!.GetValue<double>(), width = r["w"]!.GetValue<double>(), height = r["h"]!.GetValue<double>(), scale = 1 }
            };
        }
        var img = await Enviar("Page.captureScreenshot", parametros);
        await File.WriteAllBytesAsync(Path.Combine(salida, nombre + ".png"), Convert.FromBase64String(img!["data"]!.GetValue<string>()));
        Console.WriteLine($"  {nombre}.png");
    }
}
finally
{
    try { edge.Kill(true); } catch { }
}
