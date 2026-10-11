using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows.Automation;

internal sealed class ProgressCloseChecks : IDisposable
{
    private readonly string profile;
    private readonly TcpListener server = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    internal string SourceUrl { get; }

    internal ProgressCloseChecks(string profile)
    {
        this.profile = profile;
        server.Start();
        SourceUrl = $"http://127.0.0.1:{((IPEndPoint)server.LocalEndpoint).Port}/harbor-close-test.txt";
        _ = Serve();
    }

    private async Task Serve()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await server.AcceptTcpClientAsync(stop.Token);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var request = await reader.ReadLineAsync(stop.Token);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop.Token))) { }
                var body = Encoding.UTF8.GetBytes("Harbor progress-window test.\n");
                var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, stop.Token);
                if (request?.StartsWith("HEAD ") != true) await stream.WriteAsync(body, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    internal async Task Run(nint hwnd)
    {
        var owner = AutomationElement.FromHandle(hwnd);
        await Task.Run(() => ((InvokePattern)owner.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Start download"))
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke());
        AutomationElement? checkbox = null;
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (checkbox is null && DateTime.UtcNow < deadline)
        {
            checkbox = owner.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, "CloseProgressAfterOpen"));
            if (checkbox?.Current.IsOffscreen == true) checkbox = null;
            await Task.Delay(100);
        }
        if (checkbox is null) throw new Exception("Local download did not reach the completed progress view");
        if (checkbox.Current.Name != "Close this window after opening a file or folder")
            throw new Exception("Progress checkbox does not describe both actions");
        var downloaded = Path.Combine(profile, "picked-folder", "harbor-close-test.txt");
        if (File.ReadAllText(downloaded) != "Harbor progress-window test.\n")
            throw new Exception("Completed test download has unexpected contents");
        var toggle = (TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern);
        if (toggle.Current.ToggleState != ToggleState.On) throw new Exception("Default close preference changed");
        await Task.Run(toggle.Toggle);
        CheckPreference(false);
        await OpenFolder(owner);
        await Task.Delay(700);
        if (!IsWindow(hwnd)) throw new Exception("Unchecked Open folder closed the progress window");
        await Task.Run(toggle.Toggle);
        CheckPreference(true);
        await OpenFolder(owner);
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (IsWindow(hwnd) && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (IsWindow(hwnd)) throw new Exception("Checked Open folder left the progress window open");
        Console.WriteLine("PASS: completed local download; Open folder keeps the window when unchecked and closes it when checked; both preferences persist.");
    }

    private void CheckPreference(bool expected)
    {
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(profile, "preferences.json")))!;
        if (saved["CloseProgressAfterOpen"]!.GetValue<bool>() != expected)
            throw new Exception("Close-after-open preference was not saved");
    }

    private static Task OpenFolder(AutomationElement owner) => Task.Run(() =>
        ((InvokePattern)owner.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "ProgressOpenFolder"))
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke());

    public void Dispose()
    {
        stop.Cancel();
        server.Stop();
        stop.Dispose();
    }

    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
}
