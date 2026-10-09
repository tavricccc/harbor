using Microsoft.Win32;

namespace Harbor.Services;

internal static class LegacyMigration
{
    public static void Run()
    {
        if (Environment.GetEnvironmentVariable("HARBOR_DATA_DIRECTORY") is not null)
            return;
        var oldData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GopeedNative");
        if (Directory.Exists(oldData))
        {
            if (!Directory.Exists(CoreClient.DataDirectory))
                Directory.Move(oldData, CoreClient.DataDirectory);
            else
                MergeData(oldData, CoreClient.DataDirectory, Path.Combine(CoreClient.DataDirectory, "legacy-migration"));
        }

        using (var oldBackup = Registry.CurrentUser.OpenSubKey(@"Software\GopeedNative\ProtocolBackup"))
        {
            if (oldBackup is not null && !IsHarborCommand(oldBackup.GetValue("Command") as string))
            {
                using var backup = Registry.CurrentUser.CreateSubKey(@"Software\Harbor\ProtocolBackup");
                foreach (var name in oldBackup.GetValueNames())
                    if (backup.GetValue(name) is null)
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

    internal static bool IsHarborCommand(string? command) => command is not null &&
        (command.Contains("Harbor.exe", StringComparison.OrdinalIgnoreCase) || command.Contains("Gopeed.Native.exe", StringComparison.OrdinalIgnoreCase));

    private static void MergeData(string source, string destination, string conflicts)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target))
            {
                Directory.CreateDirectory(conflicts);
                target = Path.Combine(conflicts, Path.GetFileName(file));
                if (File.Exists(target))
                    target += "." + Guid.NewGuid().ToString("N");
            }
            File.Move(file, target);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            MergeData(directory, Path.Combine(destination, name), Path.Combine(conflicts, name));
        }
        Directory.Delete(source);
    }
}
