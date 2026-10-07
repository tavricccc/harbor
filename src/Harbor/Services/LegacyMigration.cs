using Microsoft.Win32;

namespace Harbor.Services;

internal static class LegacyMigration
{
    public static void Run()
    {
        if (Environment.GetEnvironmentVariable("HARBOR_DATA_DIRECTORY") is not null) return;
        var oldData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GopeedNative");
        if (!Directory.Exists(CoreClient.DataDirectory) && Directory.Exists(oldData))
            Directory.Move(oldData, CoreClient.DataDirectory);

        using (var oldBackup = Registry.CurrentUser.OpenSubKey(@"Software\GopeedNative\ProtocolBackup"))
        {
            if (oldBackup is not null)
            {
                using var backup = Registry.CurrentUser.CreateSubKey(@"Software\Harbor\ProtocolBackup");
                foreach (var name in oldBackup.GetValueNames())
                    backup.SetValue(name, oldBackup.GetValue(name)!, oldBackup.GetValueKind(name));
            }
        }
        using var startup = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (startup?.GetValue("GopeedNative") is not null)
        {
            WindowsIntegration.SetStartup(true);
            startup.DeleteValue("GopeedNative", false);
        }
        using var registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true);
        registered?.DeleteValue("GopeedNative", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\GopeedNative", false);
        foreach (var name in new[] { "Torrent", "Magnet", "Ed2k" })
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\GopeedNative." + name, false);
    }
}
