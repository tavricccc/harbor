using System.Diagnostics;
using System.Text.Json;
using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.XamlTypeInfo;

namespace HarborSetup;

/// Built in code: a single-file setup extracts compiled XAML away from the exe, where
/// WinUI never looks for it, so the setup carries no XAML of its own.
public sealed class App : Application, IXamlMetadataProvider
{
    private readonly XamlControlsXamlMetaDataProvider controls = new();
    private SetupWindow? window;

    public IXamlType GetXamlType(Type type) => controls.GetXamlType(type);
    public IXamlType GetXamlType(string fullName) => controls.GetXamlType(fullName);
    public XmlnsDefinition[] GetXmlnsDefinitions() => controls.GetXmlnsDefinitions();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs().Skip(1).Select(argument => argument.ToLowerInvariant()).ToList();
        var uninstall = arguments.Contains("/uninstall");

        // Uninstall.exe lives in the folder it deletes: hand off to a copy in %TEMP% and step aside.
        if (uninstall && !arguments.Contains("/relaunched") && Installer.IsInsideInstallDir(Environment.ProcessPath!))
        {
            RelaunchFromTemp();
            Exit();
            return;
        }

        Resources.MergedDictionaries.Add(new XamlControlsResources());
        // Harbor's own theme styles, parsed at run time because the single-file setup carries no compiled XAML.
        using (var theme = new StreamReader(typeof(App).Assembly.GetManifestResourceStream("Theme.xaml")!))
            Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(theme.ReadToEnd()));
        Strings.Initialize(SavedLanguage(), Windows.System.UserProfile.GlobalizationPreferences.Languages);
        Resources["ContentControlThemeFontFamily"] = Harbor.Services.UiFont.For(Strings.Language);
        window = new SetupWindow(uninstall, arguments.Contains("--auto-update"));
        window.Activate();
    }

    /// Harbor's saved language, so an upgrade speaks the language Harbor already uses.
    private static string SavedLanguage()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Harbor", "preferences.json");
            using var preferences = JsonDocument.Parse(File.ReadAllText(path));
            return preferences.RootElement.TryGetProperty("Language", out var language) && language.GetString() is { } id
                && Strings.SupportedLanguages.Contains(id) ? id : "";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return "";
        }
    }

    private static void RelaunchFromTemp()
    {
        var scratch = Path.Combine(Path.GetTempPath(), "Harbor-uninstall-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(scratch);
        var copy = Path.Combine(scratch, "Uninstall.exe");
        File.Copy(Environment.ProcessPath!, copy);
        var start = new ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = scratch };
        start.ArgumentList.Add("/uninstall");
        start.ArgumentList.Add("/relaunched");
        // Keep the copy's self-extraction inside the scratch folder, so one delete clears both.
        start.Environment["DOTNET_BUNDLE_EXTRACT_BASE_DIR"] = Path.Combine(scratch, "bundle");
        Process.Start(start);
    }
}
