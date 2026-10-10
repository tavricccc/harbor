using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

// Explicit local integration check: a foreground test window acts as the
// browser process and speaks the official native-messaging protocol. It never
// operates a user's browser or creates a download before confirmation.
internal static class Program
{
    private static readonly List<Process> children = [];
    private static string profile = "";
    private static string appDirectory = "";
    private static bool background;

    [STAThread]
    private static int Main(string[] args)
    {
        appDirectory = Path.GetFullPath(args[0]);
        background = args.Contains("--background");
        profile = Path.Combine(Path.GetTempPath(), "Harbor-Activation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "preferences.json"), "{\"ApiPort\":0,\"Language\":\"en-US\",\"CheckForUpdates\":false}");
        int result = 1;
        using var fixture = new Form { Text = "Harbor foreground activation test", Width = 420, Height = 180, StartPosition = FormStartPosition.CenterScreen };
        fixture.Controls.Add(new Label { Text = "Native Messaging foreground check\nNo downloads will be started.", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
        var run = new Button { Text = "Run activation checks", Dock = DockStyle.Bottom, Height = 40 };
        fixture.Controls.Add(run);
        async Task Run()
        {
            run.Enabled = false;
            try
            {
                await RunChecks(fixture);
                result = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { fixture.Close(); }
        }
        run.Click += async (_, _) => await Run();
        if (background || args.Contains("--auto")) fixture.Shown += async (_, _) => await Run();
        try { Application.Run(fixture); }
        finally
        {
            foreach (var child in children)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                child.Dispose();
            }
            Console.WriteLine("Isolated test profile: " + profile);
        }
        return result;
    }

    private static Process Start(string executable, params string[] args)
    {
        var start = new ProcessStartInfo(Path.Combine(appDirectory, executable)) { UseShellExecute = false, CreateNoWindow = true };
        start.Environment["HARBOR_DATA_DIRECTORY"] = profile;
        start.Environment["TORRENT_STORAGE_DEFAULT_FILE_IO"] = "classic";
        foreach (var arg in args) start.ArgumentList.Add(arg);
        start.RedirectStandardInput = start.RedirectStandardOutput = executable.Contains("browser-host");
        var process = Process.Start(start)!;
        children.Add(process);
        return process;
    }

    private static async Task RunChecks(Form fixture)
    {
        // Start an isolated engine first so both old and new host binaries can
        // be compared against exactly the same foreground-launch conditions.
        var core = Start("Engine/harbor-core.exe", "--data", profile, "--port", "0", "--ui", Path.Combine(appDirectory, "Harbor.exe"), "--icon", Path.Combine(appDirectory, "Assets/AppIcon.ico"));
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(Path.Combine(profile, "session.json")))
        {
            if (core.HasExited || DateTime.UtcNow > deadline) throw new Exception("Engine failed to start");
            await Task.Delay(100);
        }
        var state = JsonNode.Parse(File.ReadAllText(Path.Combine(profile, "session.json")))!;
        using var api = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{state["port"]}/api/v1/") };
        api.DefaultRequestHeaders.Add("X-Api-Token", state["token"]!.GetValue<string>());
        var config = JsonNode.Parse(await api.GetStringAsync("config"))!["data"]!;
        config["extra"] ??= new JsonObject();
        config["extra"]!["defaultDirectDownload"] = true;
        (await api.PutAsJsonAsync("config", config)).EnsureSuccessStatusCode();
        var destination = Path.Combine(profile, "picked-folder");
        Directory.CreateDirectory(destination);
        fixture.Activate();
        await Task.Delay(100);
        if (!background && GetForegroundWindow() != fixture.Handle) throw new Exception("Test fixture must own foreground before host launch");
        var host = Start("Engine/harbor-browser-host.exe");
        var seen = new HashSet<nint>();
        foreach (var mode in new[] { "cold create", "warm create", "forward POST" })
        {
            fixture.Activate();
            if (!background)
                await Task.Run(() => System.Windows.Automation.AutomationElement.FromHandle(fixture.Handle).SetFocus());
            await Task.Delay(100);
            if (!background && GetForegroundWindow() != fixture.Handle) throw new Exception("Foreground fixture lost focus before " + mode);
            var payload = new { req = new { url = "http://127.0.0.1:9/harbor-activation-test.zip" }, opts = new { path = destination } };
            object message = mode == "forward POST"
                ? new { method = "forward", @params = (object)new { path = "/api/v1/tasks?source=activation-check", method = "POST", data = payload } }
                : new { method = "create", @params = (object)Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload)) };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            await host.StandardInput.BaseStream.WriteAsync(BitConverter.GetBytes(bytes.Length));
            await host.StandardInput.BaseStream.WriteAsync(bytes);
            await host.StandardInput.BaseStream.FlushAsync();
            var header = new byte[4];
            await host.StandardOutput.BaseStream.ReadExactlyAsync(header).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
            var response = new byte[BitConverter.ToInt32(header)];
            await host.StandardOutput.BaseStream.ReadExactlyAsync(response);
            using var json = JsonDocument.Parse(response);
            if (json.RootElement.GetProperty("code").GetInt32() != 0) throw new Exception("Native host rejected request: " + Encoding.UTF8.GetString(response));
            nint confirmation = 0;
            deadline = DateTime.UtcNow.AddSeconds(15);
            while (confirmation == 0 && DateTime.UtcNow < deadline)
            {
                EnumWindows((hwnd, _) =>
                {
                    GetWindowThreadProcessId(hwnd, out var pid);
                    using var process = Process.GetProcessById((int)pid);
                    if (process.ProcessName == "Harbor" && IsWindowVisible(hwnd) && !seen.Contains(hwnd)
                        && process.MainModule?.FileName == Path.Combine(appDirectory, "Harbor.exe")) confirmation = hwnd;
                    return true;
                }, 0);
                await Task.Delay(100);
            }
            if (confirmation == 0) throw new Exception(mode + ": confirmation did not appear");
            seen.Add(confirmation);
            await Task.Delay(800); // Allow first content load and window resizing to complete.
            bool topmost = (GetWindowLongPtr(confirmation, -20).ToInt64() & 8) != 0;
            bool foreground = GetForegroundWindow() == confirmation;
            Console.WriteLine($"{mode}: visible={IsWindowVisible(confirmation)}, topmost={topmost}, foreground={foreground}");
            if (!topmost || (!background && !foreground)) throw new Exception(mode + ": confirmation did not retain the required window state");
            SetWindowPos(confirmation, new nint(-2), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
            await Task.Delay(100);
            if ((GetWindowLongPtr(confirmation, -20).ToInt64() & 8) == 0)
                throw new Exception(mode + ": native frame change removed confirmation topmost state");
        }
        var pending = Directory.GetFiles(Path.Combine(profile, "pending-downloads"), "*.json").Length;
        if (pending != 0) throw new Exception("Activation left unconsumed requests");
        await DialogChecks.Run(seen.Last(), profile);
        await DialogChecks.CancelConfirmation(seen.First());
        Console.WriteLine("PASS: cold launch, existing-instance redirection, forwarded task request; all requests consumed."
            + (background ? " Background mode: keyboard foreground was measured but not asserted." : " Keyboard foreground verified."));
    }

    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    private delegate bool EnumWindow(nint hwnd, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
}
