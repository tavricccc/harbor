using Harbor.Localization;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public sealed class CoreClient : IDisposable
{
    public static string DataDirectory { get; } = Path.GetFullPath(Environment.GetEnvironmentVariable("HARBOR_DATA_DIRECTORY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Harbor"));
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private JsonObject session = null!;
    public string ApiAddress => $"http://127.0.0.1:{session["port"]}";
    public string Token => session["token"]!.GetValue<string>();
    public int ProcessId => session["pid"]!.GetValue<int>();
    public string Version { get; private set; } = "";

    public async Task ConnectAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "session.json");
        if (File.Exists(path))
        {
            session = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            // A crashed core leaves a stale session. Restart only when its process no longer exists.
            try { using var process = Process.GetProcessById(ProcessId); if (process.ProcessName != "harbor-core") session = null!; }
            catch (ArgumentException) { session = null!; }
        }
        if (session is null)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Engine", "harbor-core.exe");
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = DataDirectory };
            start.Environment["TORRENT_STORAGE_DEFAULT_FILE_IO"] = "classic";
            start.ArgumentList.Add("--data"); start.ArgumentList.Add(DataDirectory);
            start.ArgumentList.Add("--port"); start.ArgumentList.Add(UiPreferences.Load().ApiPort.ToString());
            start.ArgumentList.Add("--ui"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Harbor.exe"));
            start.ArgumentList.Add("--icon"); start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            start.ArgumentList.Add("--language"); start.ArgumentList.Add(Strings.Language);
            using var child = Process.Start(start)!;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (child.HasExited && child.ExitCode != 0) throw new IOException(Strings.Get("Errors.CoreStart"));
                if (File.Exists(path))
                {
                    var candidate = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
                    try { using var running = Process.GetProcessById(candidate["pid"]!.GetValue<int>()); if (running.ProcessName == "harbor-core") { session = candidate; break; } } catch (ArgumentException) { /* The old session remains until the new core publishes its session. */ }
                }
                await Task.Delay(100);
            }
            if (session is null) throw new TimeoutException(Strings.Get("Errors.CoreTimeout"));
        }
        http.BaseAddress = new Uri(ApiAddress + "/api/v1/");
        http.DefaultRequestHeaders.Add("X-Api-Token", Token);
        http.DefaultRequestHeaders.Add("X-Gopeed-Native-Confirmed", "1");
        using var languageRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{session["controlPort"]}/language")
        {
            Content = new StringContent(new JsonObject { ["language"] = Strings.Language }.ToJsonString(), Encoding.UTF8, "application/json")
        };
        using var languageResponse = await http.SendAsync(languageRequest);
        languageResponse.EnsureSuccessStatusCode();
        Version = (await GetAsync("info"))!["version"]!.GetValue<string>();
    }

    public Task<JsonNode?> GetAsync(string route, CancellationToken cancellationToken = default) => SendAsync(HttpMethod.Get, route, cancellationToken: cancellationToken);
    public async Task<T?> GetAsync<T>(string route, CancellationToken cancellationToken = default) where T : class
        => (await GetAsync(route, cancellationToken))?.Deserialize<T>(System.Text.Json.JsonSerializerOptions.Web);
    public async Task<JsonNode?> SendAsync(HttpMethod method, string route, JsonNode? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = (await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false))!.AsObject();
        if (result["code"]!.GetValue<int>() != 0) throw new DownloadApiException(result["msg"]!.GetValue<string>());
        result.Remove("data", out var data);
        return data;
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
