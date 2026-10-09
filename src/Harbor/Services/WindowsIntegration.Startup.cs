using Harbor.Localization;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public static partial class WindowsIntegration
{
    public static bool StartsWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Harbor") is not null;
        }
    }
    public static void SetStartup(bool enabled)
    {
        var prefs = UiPreferences.Load();
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
            key.SetValue("Harbor", $"\"{Path.Combine(AppContext.BaseDirectory, "Engine", "harbor-core.exe")}\" --data \"{CoreClient.DataDirectory}\" --ui \"{Path.Combine(AppContext.BaseDirectory, "Harbor.exe")}\" --icon \"{Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")}\" --port {prefs.ApiPort} --language {Strings.Language}");
        else
            key.DeleteValue("Harbor", false);
        prefs.StartWithWindows = enabled;
        prefs.Save();
    }
    private static void RemoveStartup()
    {
        using var startup = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        startup?.DeleteValue("Harbor", false);
    }
}
