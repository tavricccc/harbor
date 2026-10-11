using Harbor.Localization;
using System.Globalization;

namespace Harbor.Services;

internal static class IntegrationCommands
{
    internal static bool TryRun(string[] args)
    {
        if (!args.Contains("--register-integrations") && !args.Contains("--unregister-integrations"))
            return false;

        LegacyMigration.Run();
        var preferences = UiPreferences.Load();
        Strings.Initialize(preferences.Language, [CultureInfo.CurrentUICulture.Name]);
        if (args.Contains("--unregister-integrations"))
            WindowsIntegration.Unregister();
        else
        {
            WindowsIntegration.InstallBrowserHost();
            WindowsIntegration.RegisterFileTypes();
            WindowsIntegration.SetStartup(preferences.StartWithWindows);
        }
        return true;
    }
}
