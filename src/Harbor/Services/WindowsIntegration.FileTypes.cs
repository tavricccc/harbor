using Harbor.Localization;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public static partial class WindowsIntegration
{
    public static void RegisterFileTypes()
    {
        using var capability = Registry.CurrentUser.CreateSubKey(@"Software\Harbor\Capabilities");
        capability.SetValue("ApplicationName", "Harbor");
        capability.SetValue("ApplicationDescription", Strings.Get("App.Description"));
        using var files = capability.CreateSubKey("FileAssociations");
        files.SetValue(".torrent", "Harbor.Torrent");
        using var links = capability.CreateSubKey("URLAssociations");
        links.SetValue("magnet", "Harbor.Magnet");
        links.SetValue("ed2k", "Harbor.Ed2k");
        using var registered = Registry.CurrentUser.CreateSubKey("Software\\RegisteredApplications");
        registered.SetValue("Harbor", @"Software\Harbor\Capabilities");
        foreach (var name in new[] { "Torrent", "Magnet", "Ed2k" })
        {
            using var type = Registry.CurrentUser.CreateSubKey("Software\\Classes\\Harbor." + name);
            type.SetValue("", "Harbor");
            if (name != "Torrent")
                type.SetValue("URL Protocol", "");
            using var icon = type.CreateSubKey("DefaultIcon");
            icon.SetValue("", Path.Combine(AppContext.BaseDirectory, "Harbor.exe") + ",0");
            using var command = type.CreateSubKey(@"shell\open\command");
            command.SetValue("", $"\"{Path.Combine(AppContext.BaseDirectory, "Harbor.exe")}\" \"%1\"");
        }
    }
    private static void RemoveFileTypes()
    {
        using var registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true);
        registered?.DeleteValue("Harbor", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Harbor\Capabilities", false);
        foreach (var name in new[] { "Torrent", "Magnet", "Ed2k" })
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Harbor." + name, false);
    }
    public static void OpenDefaultApps() => Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=Harbor") { UseShellExecute = true });
}
