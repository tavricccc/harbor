using Harbor.Localization;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public sealed partial class CoreClient
{
    public async Task ConnectAsync()
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "session.json");
        if (File.Exists(path))
        {
            session = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            // A crashed core leaves a stale session. Restart only when its process no longer exists.
            try
            {
                using var process = Process.GetProcessById(ProcessId);
                if (process.ProcessName != "harbor-core")
                    session = null!;
            }
            catch (ArgumentException) { session = null!; }
        }
        if (session is null)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Engine", "harbor-core.exe");
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = DataDirectory };
            start.Environment["TORRENT_STORAGE_DEFAULT_FILE_IO"] = "classic";
            start.ArgumentList.Add("--data");
            start.ArgumentList.Add(DataDirectory);
            start.ArgumentList.Add("--port");
            start.ArgumentList.Add(UiPreferences.Load().ApiPort.ToString());
            start.ArgumentList.Add("--ui");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Harbor.exe"));
            start.ArgumentList.Add("--icon");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            start.ArgumentList.Add("--language");
            start.ArgumentList.Add(Strings.Language);
            // Wake as soon as the core publishes its session; polling remains the fallback.
            var published = new SemaphoreSlim(0);
            using var watcher = new FileSystemWatcher(DataDirectory, "session.json");
            watcher.Created += (_, _) => published.Release();
            watcher.Changed += (_, _) => published.Release();
            watcher.Renamed += (_, _) => published.Release();
            watcher.EnableRaisingEvents = true;
            using var child = Process.Start(start)!;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (child.HasExited && child.ExitCode != 0)
                    throw new IOException(Strings.Get("Errors.CoreStart"));
                if (File.Exists(path))
                {
                    var candidate = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
                    try
                    {
                        using var running = Process.GetProcessById(candidate["pid"]!.GetValue<int>());
                        if (running.ProcessName == "harbor-core")
                        {
                            session = candidate;
                            break;
                        }
                    }
                    catch (ArgumentException) { /* The old session remains until the new core publishes its session. */ }
                }
                await published.WaitAsync(100);
            }
            if (session is null)
                throw new TimeoutException(Strings.Get("Errors.CoreTimeout"));
        }
        http.BaseAddress = new Uri(ApiAddress + "/api/v1/");
        http.DefaultRequestHeaders.Add("X-Api-Token", Token);
        http.DefaultRequestHeaders.Add("X-Gopeed-Native-Confirmed", "1");
        using var languageRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{session["controlPort"]}/language")
        {
            Content = new StringContent(new JsonObject { ["language"] = Strings.Language }.ToJsonString(), Encoding.UTF8, "application/json")
        };
        var language = http.SendAsync(languageRequest);
        var info = GetAsync("info");
        await Task.WhenAll(language, info);
        using var languageResponse = language.Result;
        languageResponse.EnsureSuccessStatusCode();
        Version = info.Result!["version"]!.GetValue<string>();
    }
}
