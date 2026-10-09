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
                await Task.Delay(100);
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
        using var languageResponse = await http.SendAsync(languageRequest);
        languageResponse.EnsureSuccessStatusCode();
        Version = (await GetAsync("info"))!["version"]!.GetValue<string>();
    }
}
