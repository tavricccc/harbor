using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Harbor.Localization;
using Microsoft.Win32;

namespace HarborSetup;

/// Per-user installation: files under %LOCALAPPDATA%\Programs, registrations under HKCU.
internal static partial class Installer
{
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Harbor";
    // Inno Setup's entry from Harbor 0.11 and earlier; upgrades take its location and remove it.
    private const string InnoUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{B652BEF3-0741-4B5E-9066-C6F3EBF18622}_is1";
    private const string ProtocolKey = @"Software\Classes\gopeed";
    private const string ProtocolBackupKey = @"Software\Harbor\ProtocolBackup";
    private const string Manifest = "installed-files.txt";

    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string DefaultInstallDir => Path.Combine(LocalAppData, "Programs", "Harbor");
    private static string LegacyInstallDir => Path.Combine(LocalAppData, "Programs", "Gopeed Native");
    private static string DataDir => Path.Combine(LocalAppData, "Harbor");
    private static string LegacyDataDir => Path.Combine(LocalAppData, "GopeedNative");
    private static string StartMenuDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Harbor");
    private static string StartMenuShortcut => Path.Combine(StartMenuDir, "Harbor.lnk");
    private static string DesktopShortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Harbor.lnk");

    /// The previous location, from this setup's entry or Inno Setup's.
    private static string? PreviousInstallDir
    {
        get
        {
            foreach (var key in new[] { UninstallKey, InnoUninstallKey })
            {
                using var entry = Registry.CurrentUser.OpenSubKey(key);
                if (entry?.GetValue("InstallLocation") is string location && location.Length > 0)
                    return Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
            }
            return null;
        }
    }

    /// Keep a custom location, but move the original Gopeed Native default to Harbor's.
    public static string InstallDir { get; } =
        PreviousInstallDir is { } previous && IsManagedInstallation(previous) && !SamePath(previous, LegacyInstallDir)
            ? previous : DefaultInstallDir;

    private static string AppExe => Path.Combine(InstallDir, "Harbor.exe");
    private static string UninstallerExe => Path.Combine(InstallDir, "Uninstall.exe");

    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static bool IsInstalled => File.Exists(AppExe);
    public static bool HasDesktopShortcut => File.Exists(DesktopShortcut);

    public static bool IsInsideInstallDir(string path) =>
        Path.GetFullPath(path).StartsWith(InstallDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static string License
    {
        get
        {
            using var zip = OpenPayload();
            using var reader = new StreamReader(zip.GetEntry("LICENSE-Gopeed.txt")!.Open());
            return reader.ReadToEnd();
        }
    }

    public static void Install(bool desktopShortcut)
    {
        var previous = PreviousInstallDir;
        StopInstalledVersions(previous);
        BackupProtocol();

        using (var zip = OpenPayload())
        {
            using var manifest = new StreamReader(zip.GetEntry(Manifest)!.Open());
            var current = manifest.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (IsManagedInstallation(InstallDir))
                CleanInstallationTree(InstallDir, "", current);
            Directory.CreateDirectory(InstallDir);
            foreach (var entry in zip.Entries.Where(entry => entry.Name.Length > 0))
            {
                var target = Path.GetFullPath(Path.Combine(InstallDir, entry.FullName));
                if (!IsInsideInstallDir(target))
                    throw new InvalidDataException(entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Retry(target, () => entry.ExtractToFile(target, overwrite: true));
            }
        }
        if (!SamePath(Environment.ProcessPath!, UninstallerExe))
            Retry(UninstallerExe, () => File.Copy(Environment.ProcessPath!, UninstallerExe, overwrite: true));
        // Inno Setup's uninstaller and its log.
        foreach (var file in Directory.EnumerateFiles(InstallDir, "unins0*"))
            Retry(file, () => File.Delete(file));

        ShellLink.Create(StartMenuShortcut, AppExe, InstallDir);
        if (desktopShortcut)
            ShellLink.Create(DesktopShortcut, AppExe, InstallDir);
        else if (File.Exists(DesktopShortcut))
            File.Delete(DesktopShortcut);

        using (var protocol = Registry.CurrentUser.CreateSubKey(ProtocolKey))
        {
            protocol.SetValue("", "URL:Harbor Protocol");
            protocol.SetValue("URL Protocol", "");
        }
        using (var icon = Registry.CurrentUser.CreateSubKey(ProtocolKey + @"\DefaultIcon"))
            icon.SetValue("", AppExe + ",0");
        using (var command = Registry.CurrentUser.CreateSubKey(ProtocolKey + @"\shell\open\command"))
            command.SetValue("", $"\"{AppExe}\" \"%1\"");

        RegisterIntegrations();
        if (previous is not null)
            RemoveOldInstallation(previous);
        RemoveOldInstallation(LegacyInstallDir);
        RemoveLegacyShortcuts();

        using (var entry = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            entry.SetValue("DisplayName", "Harbor");
            entry.SetValue("DisplayVersion", Version);
            entry.SetValue("Publisher", "Tavric");
            entry.SetValue("DisplayIcon", AppExe);
            entry.SetValue("InstallLocation", InstallDir);
            entry.SetValue("UninstallString", $"\"{UninstallerExe}\" /uninstall");
            entry.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            entry.SetValue("EstimatedSize", (int)(DirectorySize(InstallDir) / 1024), RegistryValueKind.DWord);
            entry.SetValue("NoModify", 1, RegistryValueKind.DWord);
            entry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        Registry.CurrentUser.DeleteSubKeyTree(InnoUninstallKey, throwOnMissingSubKey: false);
    }

    public static void Launch() =>
        Process.Start(new ProcessStartInfo(AppExe) { UseShellExecute = true, WorkingDirectory = InstallDir });

    private static ZipArchive OpenPayload() =>
        new(Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("This setup was built without the Harbor app inside."));

    private static void RegisterIntegrations()
    {
        Process process;
        try
        {
            process = Process.Start(new ProcessStartInfo(AppExe, "--register-integrations") { UseShellExecute = false, CreateNoWindow = true })!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(Strings.Get("Installer.RegistrationLaunchFailed"));
        }
        using (process)
        {
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException(Strings.Format("Installer.RegistrationFailed", process.ExitCode));
        }
        VerifyBrowserHost(@"Software\Google\Chrome\NativeMessagingHosts", "browser-host.json");
        VerifyBrowserHost(@"Software\Microsoft\Edge\NativeMessagingHosts", "browser-host.json");
        VerifyBrowserHost(@"Software\Mozilla\NativeMessagingHosts", "browser-host-firefox.json");
    }

    private static void VerifyBrowserHost(string browserKey, string manifestName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(browserKey + @"\com.gopeed.gopeed");
        if (key?.GetValue("") is not string manifest)
            throw new InvalidOperationException(Strings.Format("Installer.RegistrationMissing", browserKey));
        if (!SamePath(manifest, Path.Combine(DataDir, manifestName)))
            throw new InvalidOperationException(Strings.Format("Installer.RegistrationOutdated", manifest));
        if (!File.Exists(manifest))
            throw new InvalidOperationException(Strings.Get("Installer.ManifestMissing"));
    }

    /// Remember another application's gopeed: handler once, so uninstall can give it back.
    private static void BackupProtocol()
    {
        using var existing = Registry.CurrentUser.OpenSubKey(ProtocolBackupKey);
        if (existing is not null)
            return;
        using var command = Registry.CurrentUser.OpenSubKey(ProtocolKey + @"\shell\open\command");
        if (command?.GetValue("") is not string value || IsHarborCommand(value))
            return;
        using var backup = Registry.CurrentUser.CreateSubKey(ProtocolBackupKey);
        backup.SetValue("Command", value);
        using (var protocol = Registry.CurrentUser.OpenSubKey(ProtocolKey))
            if (protocol?.GetValue("") is string description)
                backup.SetValue("Description", description);
        using (var icon = Registry.CurrentUser.OpenSubKey(ProtocolKey + @"\DefaultIcon"))
            if (icon?.GetValue("") is string location)
                backup.SetValue("Icon", location);
    }

    private static bool IsHarborCommand(string command) =>
        command.Contains("harbor.exe", StringComparison.OrdinalIgnoreCase) || command.Contains("gopeed.native.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsManagedInstallation(string directory) =>
        Path.GetPathRoot(directory) is { } root && !SamePath(directory, root)
        && (File.Exists(Path.Combine(directory, "Harbor.exe")) || File.Exists(Path.Combine(directory, "Gopeed.Native.exe")));

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);

    private static long DirectorySize(string path) =>
        new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
}
