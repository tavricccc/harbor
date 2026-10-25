using System.Diagnostics;
using Harbor.Localization;
using Microsoft.Win32;

namespace HarborSetup;

internal static partial class Installer
{
    private static readonly string[] OwnedProcesses =
        [@"harbor.exe", @"gopeed.native.exe", @"engine\harbor-core.exe", @"engine\gopeed-core.exe", @"engine\harbor-browser-host.exe", @"engine\gopeed-browser-host.exe"];

    public static void Uninstall()
    {
        StopInstalledVersions(null);
        if (File.Exists(AppExe))
            using (var process = Process.Start(new ProcessStartInfo(AppExe, "--unregister-integrations") { UseShellExecute = false, CreateNoWindow = true }))
                process?.WaitForExit();

        Registry.CurrentUser.DeleteSubKeyTree(ProtocolKey, throwOnMissingSubKey: false);
        using (var backup = Registry.CurrentUser.OpenSubKey(ProtocolBackupKey))
            if (backup?.GetValue("Command") is string command && !IsHarborCommand(command))
            {
                using (var protocol = Registry.CurrentUser.CreateSubKey(ProtocolKey))
                {
                    protocol.SetValue("URL Protocol", "");
                    if (backup.GetValue("Description") is string description)
                        protocol.SetValue("", description);
                }
                using (var open = Registry.CurrentUser.CreateSubKey(ProtocolKey + @"\shell\open\command"))
                    open.SetValue("", command);
                if (backup.GetValue("Icon") is string icon)
                    using (var iconKey = Registry.CurrentUser.CreateSubKey(ProtocolKey + @"\DefaultIcon"))
                        iconKey.SetValue("", icon);
            }
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Harbor", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\GopeedNative", throwOnMissingSubKey: false);
        foreach (var (key, name) in new[] { (@"Software\RegisteredApplications", "Harbor"), (@"Software\RegisteredApplications", "GopeedNative"),
            (@"Software\Microsoft\Windows\CurrentVersion\Run", "Harbor"), (@"Software\Microsoft\Windows\CurrentVersion\Run", "GopeedNative") })
            using (var values = Registry.CurrentUser.OpenSubKey(key, writable: true))
                values?.DeleteValue(name, throwOnMissingValue: false);
        RemoveOwnedRegistrations();
        RemoveLegacyShortcuts();
        RemoveOldInstallation(LegacyInstallDir);
        RemoveApplicationState(DataDir);
        RemoveApplicationState(LegacyDataDir);

        if (File.Exists(DesktopShortcut))
            File.Delete(DesktopShortcut);
        if (File.Exists(StartMenuShortcut))
            File.Delete(StartMenuShortcut);
        RemoveEmptyDirectory(StartMenuDir);
        if (IsManagedInstallation(InstallDir) || File.Exists(UninstallerExe))
        {
            CleanInstallationTree(InstallDir, "", new HashSet<string>());
            RemoveEmptyDirectory(InstallDir);
        }
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(InnoUninstallKey, throwOnMissingSubKey: false);
    }

    /// What can only go once this process exits: the uninstaller's temp copy and the
    /// single-file extraction folder. A hidden shell waits a moment, then removes them.
    public static void ScheduleSelfCleanup()
    {
        var targets = new List<string> { Path.Combine(Path.GetTempPath(), ".net", "HarborSetup") };
        var own = Path.GetDirectoryName(Environment.ProcessPath!)!;
        if (Path.GetFileName(own).StartsWith("Harbor-uninstall-", StringComparison.OrdinalIgnoreCase))
            targets.Add(own);
        var removals = string.Join(" & ", targets.Select(directory => $"rmdir /s /q \"{directory}\""));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c ping -n 4 127.0.0.1 >nul & {removals}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetTempPath(),
        });
    }

    private static void StopInstalledVersions(string? previous)
    {
        StopInstallation(InstallDir, DataDir);
        if (previous is not null && !SamePath(previous, InstallDir))
            StopInstallation(previous, DataDir);
        StopInstallation(LegacyInstallDir, LegacyDataDir);
    }

    /// Asks the core to save and quit, then stops only executables inside this installation,
    /// including browser-held hosts.
    private static void StopInstallation(string directory, string dataDirectory)
    {
        if (!IsManagedInstallation(directory))
            return;
        var core = Path.Combine(directory, "Engine", "harbor-core.exe");
        if (!File.Exists(core))
            core = Path.Combine(directory, "Engine", "gopeed-core.exe");
        if (File.Exists(core))
        {
            var start = new ProcessStartInfo(core) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--data");
            start.ArgumentList.Add(dataDirectory);
            start.ArgumentList.Add("--shutdown");
            using var shutdown = Process.Start(start);
            shutdown?.WaitForExit(30000);
        }
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        foreach (var name in OwnedProcesses.Select(Path.GetFileNameWithoutExtension).Distinct())
            foreach (var process in Process.GetProcessesByName(name))
                using (process)
                    try
                    {
                        if (process.MainModule?.FileName is { } path && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            && OwnedProcesses.Contains(path[prefix.Length..].ToLowerInvariant()))
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                    }
                    catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
                    {
                        // Gone already, or not ours to inspect.
                    }
    }

    /// Walks only owned installation trees, never following directory junctions, and removes
    /// every file the new version does not ship.
    private static void CleanInstallationTree(string directory, string relative, IReadOnlySet<string> current)
    {
        if (!Directory.Exists(directory))
            return;
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            var child = relative + entry.Name;
            if (entry is DirectoryInfo)
            {
                if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    CleanInstallationTree(entry.FullName, child + Path.DirectorySeparatorChar, current);
                RemoveEmptyDirectory(entry.FullName);
            }
            else if (!current.Contains(child))
                Retry(entry.FullName, () => File.Delete(entry.FullName));
        }
    }

    private static void RemoveOldInstallation(string directory)
    {
        if (SamePath(directory, InstallDir) || !IsManagedInstallation(directory))
            return;
        CleanInstallationTree(directory, "", new HashSet<string>());
        RemoveEmptyDirectory(directory);
    }

    private static void RemoveLegacyShortcuts()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var shortcut in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Gopeed Native.lnk"),
            Path.Combine(programs, "Gopeed Native", "Gopeed Native.lnk"),
            Path.Combine(programs, "Gopeed Native", "Uninstall Gopeed Native.lnk"),
        })
            if (File.Exists(shortcut))
                File.Delete(shortcut);
        RemoveEmptyDirectory(Path.Combine(programs, "Gopeed Native"));
    }

    private static void RemoveOwnedRegistrations()
    {
        foreach (var name in new[] { "Harbor.Torrent", "Harbor.Magnet", "Harbor.Ed2k", "GopeedNative.Torrent", "GopeedNative.Magnet", "GopeedNative.Ed2k" })
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + name, throwOnMissingSubKey: false);
        foreach (var browser in new[] { @"Software\Google\Chrome", @"Software\Microsoft\Edge", @"Software\Mozilla" })
        {
            var key = browser + @"\NativeMessagingHosts\com.gopeed.gopeed";
            using (var host = Registry.CurrentUser.OpenSubKey(key))
                if (host?.GetValue("") is not string manifest
                    || !(SamePath(Path.GetDirectoryName(manifest)!, DataDir) || SamePath(Path.GetDirectoryName(manifest)!, LegacyDataDir)))
                    continue;
            Registry.CurrentUser.DeleteSubKeyTree(key, throwOnMissingSubKey: false);
        }
    }

    /// Deletes owned metadata, never a user's selected download directory.
    private static void RemoveApplicationState(string dataDirectory)
    {
        string[] files = ["api-token", "session.json", "session.json.tmp", "preferences.json", "preferences.json.tmp", "theme.txt",
            "deferred-downloads.json", "deferred-downloads.json.tmp", "gopeed.db", ".torrent.bolt.db",
            "browser-host.json", "browser-host-firefox.json", "browser-integration-backup.json", "frontend-error.log"];
        string[] directories = ["logs", "pending-downloads", "extensions", "webview", "legacy-migration"];
        foreach (var file in files.Select(name => Path.Combine(dataDirectory, name)).Where(File.Exists))
            Retry(file, () => File.Delete(file));
        foreach (var directory in directories.Select(name => Path.Combine(dataDirectory, name)).Where(Directory.Exists))
            Retry(directory, () => Directory.Delete(directory, recursive: true));
        RemoveEmptyDirectory(dataDirectory);
    }

    private static void RemoveEmptyDirectory(string directory)
    {
        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            Directory.Delete(directory);
    }

    /// Retries briefly: a process that just exited can hold its files for a moment.
    private static void Retry(string path, Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20)
                    throw new IOException(Strings.Format("Installer.CleanupFailed", path), error);
                Thread.Sleep(250);
            }
        }
    }
}
