using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Gopeed_Native.Services;

public static class WindowsIntegration
{
    private const string HostName = "com.gopeed.gopeed";
    private static string BackupPath => Path.Combine(CoreClient.DataDirectory, "browser-integration-backup.json");
    private static readonly string[] BrowserKeys = [@"Software\Google\Chrome\NativeMessagingHosts", @"Software\Microsoft\Edge\NativeMessagingHosts", @"Software\Mozilla\NativeMessagingHosts"];
    public static bool IsBrowserHostRegistered()
    {
        var host = Path.Combine(AppContext.BaseDirectory, "Engine", "gopeed-browser-host.exe");
        foreach (var path in BrowserKeys)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path + "\\" + HostName);
            if (key?.GetValue("") is not string manifest || !File.Exists(manifest)) return false;
            if (!string.Equals(JsonNode.Parse(File.ReadAllText(manifest))?["path"]?.GetValue<string>(), host, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
    public static bool StartsWithWindows { get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("GopeedNative") is not null; } }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("GopeedNative", $"\"{Path.Combine(AppContext.BaseDirectory, "Engine", "gopeed-core.exe")}\" --data \"{CoreClient.DataDirectory}\" --ui \"{Path.Combine(AppContext.BaseDirectory, "Gopeed.Native.exe")}\" --icon \"{Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")}\" --port {UiPreferences.Load().ApiPort}"); else key.DeleteValue("GopeedNative", false);
    }
    public static void InstallBrowserHost()
    {
        var host = Path.Combine(AppContext.BaseDirectory, "Engine", "gopeed-browser-host.exe");
        if (!File.Exists(host)) throw new FileNotFoundException("請重新安裝程式以啟用瀏覽器接管。");
        Directory.CreateDirectory(CoreClient.DataDirectory);
        if (!File.Exists(BackupPath))
        {
            var backup = new Dictionary<string, string?>();
            foreach (var path in BrowserKeys) { using var key = Registry.CurrentUser.OpenSubKey(path + "\\" + HostName); backup[path] = key?.GetValue("") as string; }
            File.WriteAllText(BackupPath, JsonSerializer.Serialize(backup));
        }
        for (var index = 0; index < BrowserKeys.Length; index++)
        {
            var file = Path.Combine(CoreClient.DataDirectory, index == 2 ? "browser-host-firefox.json" : "browser-host.json");
            var manifest = new JsonObject { ["name"] = HostName, ["description"] = "Harbor", ["path"] = host, ["type"] = "stdio" };
            if (index == 2) manifest["allowed_extensions"] = ConfigJson.Array(["{c5d69a8f-2ed0-46a7-afa4-b3a00dc58088}"]);
            else manifest["allowed_origins"] = ConfigJson.Array(["chrome-extension://mijpgljlfcapndmchhjffkpckknofcnd/", "chrome-extension://dkajnckekendchdleoaenoophcobooce/"]);
            File.WriteAllText(file, manifest.ToJsonString());
            using var key = Registry.CurrentUser.CreateSubKey(BrowserKeys[index] + "\\" + HostName); key.SetValue("", file);
        }
    }
    public static void RegisterFileTypes()
    {
        using var capability = Registry.CurrentUser.CreateSubKey(@"Software\GopeedNative\Capabilities"); capability.SetValue("ApplicationName", "Harbor"); capability.SetValue("ApplicationDescription", "下載管理員");
        using var files = capability.CreateSubKey("FileAssociations"); files.SetValue(".torrent", "GopeedNative.Torrent");
        using var links = capability.CreateSubKey("URLAssociations"); links.SetValue("magnet", "GopeedNative.Magnet"); links.SetValue("ed2k", "GopeedNative.Ed2k");
        using var registered = Registry.CurrentUser.CreateSubKey("Software\\RegisteredApplications"); registered.SetValue("GopeedNative", @"Software\GopeedNative\Capabilities");
        foreach (var name in new[] { "Torrent", "Magnet", "Ed2k" })
        {
            using var type = Registry.CurrentUser.CreateSubKey("Software\\Classes\\GopeedNative." + name); type.SetValue("", "Harbor");
            if (name != "Torrent") type.SetValue("URL Protocol", "");
            using var icon = type.CreateSubKey("DefaultIcon"); icon.SetValue("", Path.Combine(AppContext.BaseDirectory, "Gopeed.Native.exe") + ",0");
            using var command = type.CreateSubKey(@"shell\open\command"); command.SetValue("", $"\"{Path.Combine(AppContext.BaseDirectory, "Gopeed.Native.exe")}\" \"%1\"");
        }
    }
    public static void Unregister()
    {
        var backup = File.Exists(BackupPath) ? JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(BackupPath))! : new();
        for (var index = 0; index < BrowserKeys.Length; index++)
        {
            var expected = Path.Combine(CoreClient.DataDirectory, index == 2 ? "browser-host-firefox.json" : "browser-host.json");
            var path = BrowserKeys[index] + "\\" + HostName;
            using var key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key?.GetValue("") as string != expected) continue;
            if (backup.TryGetValue(BrowserKeys[index], out var previous) && previous is not null) key.SetValue("", previous); else Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        using var registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true); registered?.DeleteValue("GopeedNative", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\GopeedNative\Capabilities", false);
        foreach (var name in new[] { "Torrent", "Magnet", "Ed2k" }) Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\GopeedNative." + name, false);
        using var startup = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true); startup?.DeleteValue("GopeedNative", false);
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
    }
    public static void OpenDefaultApps() => Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=GopeedNative") { UseShellExecute = true });
}
