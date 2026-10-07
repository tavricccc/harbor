using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace Gopeed_Native.Services;

public sealed class CoreClient : IDisposable
{
    public static string DataDirectory { get; } = Path.GetFullPath(Environment.GetEnvironmentVariable("HARBOR_DATA_DIRECTORY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GopeedNative"));
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private JsonObject session = null!;
    public string ApiAddress => $"http://127.0.0.1:{session["port"]}";
    public string Token => session["token"]!.GetValue<string>();
    public int ProcessId => session["pid"]!.GetValue<int>();

    public async Task ConnectAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "session.json");
        if (File.Exists(path))
        {
            session = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            // A crashed core leaves a stale session. Restart only when its process no longer exists.
            try { using var process = Process.GetProcessById(ProcessId); if (process.ProcessName != "gopeed-core") session = null!; }
            catch (ArgumentException) { session = null!; }
        }
        if (session is null)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Engine", "gopeed-core.exe");
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = DataDirectory };
            start.Environment["TORRENT_STORAGE_DEFAULT_FILE_IO"] = "classic";
            start.ArgumentList.Add("--data"); start.ArgumentList.Add(DataDirectory);
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(UiPreferences.Load().ApiPort.ToString());
            start.ArgumentList.Add("--ui"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Gopeed.Native.exe"));
            start.ArgumentList.Add("--icon"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            using var child = Process.Start(start)!;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (child.HasExited && child.ExitCode != 0) throw new IOException("無法啟動下載服務。請查看記錄資料夾。");
                if (File.Exists(path))
                {
                    var candidate = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
                    try { using var running = Process.GetProcessById(candidate["pid"]!.GetValue<int>()); if (running.ProcessName == "gopeed-core") { session = candidate; break; } } catch (ArgumentException) { /* The old session remains until the new core publishes its session. */ }
                }
                await Task.Delay(100);
            }
            if (session is null) throw new TimeoutException("下載核心啟動逾時。");
        }
        http.BaseAddress = new Uri(ApiAddress + "/api/v1/");
        http.DefaultRequestHeaders.Add("X-Api-Token", Token);
        http.DefaultRequestHeaders.Add("X-Gopeed-Native-Confirmed", "1");
        await GetAsync("info");
    }

    public Task<JsonNode?> GetAsync(string route) => SendAsync(HttpMethod.Get, route);
    public async Task<JsonNode?> SendAsync(HttpMethod method, string route, JsonNode? body = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        if (result["code"]!.GetValue<int>() != 0) throw new DownloadApiException(result["msg"]!.GetValue<string>());
        return result["data"]?.DeepClone();
    }
    public async Task<string> FetchTextAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "proxy");
        request.Headers.Add("X-Target-Uri", url);
        using var response = await http.SendAsync(request); response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task StopAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{session["controlPort"]}/shutdown");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    public void Dispose() => http.Dispose();
}
