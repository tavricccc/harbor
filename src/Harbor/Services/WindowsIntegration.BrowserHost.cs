using Harbor.Localization;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public static partial class WindowsIntegration
{
    private const string HostName = "com.gopeed.gopeed";
    private static string BackupPath => Path.Combine(CoreClient.DataDirectory, "browser-integration-backup.json");
    private static readonly string[] BrowserKeys = [@"Software\Google\Chrome\NativeMessagingHosts", @"Software\Microsoft\Edge\NativeMessagingHosts", @"Software\Mozilla\NativeMessagingHosts"];
    public static bool IsBrowserHostRegistered()
    {
        var host = Path.Combine(AppContext.BaseDirectory, "Engine", "harbor-browser-host.exe");
        foreach (var path in BrowserKeys)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path + "\\" + HostName);
            if (key?.GetValue("") is not string manifest || !File.Exists(manifest)) return false;
            if (!string.Equals(JsonNode.Parse(File.ReadAllText(manifest))?["path"]?.GetValue<string>(), host, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }
    public static void InstallBrowserHost()
    {
        var host = Path.Combine(AppContext.BaseDirectory, "Engine", "harbor-browser-host.exe");
        if (!File.Exists(host)) throw new FileNotFoundException(Strings.Get("Errors.BrowserReinstall"));
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
    private static void RemoveBrowserHost()
    {
        var backup = File.Exists(BackupPath) ? JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(BackupPath))! : new();
        for (var index = 0; index < BrowserKeys.Length; index++)
        {
            var expected = Path.Combine(CoreClient.DataDirectory, index == 2 ? "browser-host-firefox.json" : "browser-host.json");
            var path = BrowserKeys[index] + "\\" + HostName;
            using var key = Registry.CurrentUser.OpenSubKey(path, true);
            if (key?.GetValue("") as string != expected) continue;
            if (backup.TryGetValue(BrowserKeys[index], out var previous) && previous is not null && File.Exists(previous) && !IsOwnManifest(previous))
                key.SetValue("", previous);
            else Registry.CurrentUser.DeleteSubKeyTree(path, false);
        }
        if (File.Exists(BackupPath)) File.Delete(BackupPath);
        foreach (var name in new[] { "browser-host.json", "browser-host-firefox.json" })
            File.Delete(Path.Combine(CoreClient.DataDirectory, name));
    }
    private static bool IsOwnManifest(string path)
    {
        var legacyData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GopeedNative");
        return string.Equals(Path.GetDirectoryName(path), CoreClient.DataDirectory, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetDirectoryName(path), legacyData, StringComparison.OrdinalIgnoreCase);
    }
}
