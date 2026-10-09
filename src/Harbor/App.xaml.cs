using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppLifecycle;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Harbor;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private AppInstance? registeredInstance;
    private readonly HashSet<Views.DownloadWindow> downloadWindows = [];
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            System.IO.Directory.CreateDirectory(Services.CoreClient.DataDirectory);
            System.IO.File.AppendAllText(System.IO.Path.Combine(Services.CoreClient.DataDirectory, "frontend-error.log"), $"{DateTimeOffset.Now}\n{e.Exception}\n");
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs();
        Services.LegacyMigration.Run();
        Strings.Initialize(Services.UiPreferences.Load().Language, Windows.System.UserProfile.GlobalizationPreferences.Languages);
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = Strings.Language;
        if (commandLine.Contains("--register-integrations"))
        {
            Services.WindowsIntegration.InstallBrowserHost();
            Services.WindowsIntegration.RegisterFileTypes();
            if (Services.WindowsIntegration.StartsWithWindows) Services.WindowsIntegration.SetStartup(true);
            Exit(); return;
        }
        if (commandLine.Contains("--unregister-integrations")) { Services.WindowsIntegration.Unregister(); Exit(); return; }
        if (!Services.WindowsIntegration.IsBrowserHostRegistered()) Services.WindowsIntegration.InstallBrowserHost();
        var profileKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Services.CoreClient.DataDirectory.ToUpperInvariant())));
        var instance = AppInstance.FindOrRegisterForKey("Harbor.Main." + profileKey);
        if (!instance.IsCurrent)
        {
            Services.WindowActivation.AllowRedirect(instance.ProcessId);
            await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit(); return;
        }
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        registeredInstance = instance;
        instance.Activated += (_, activation) => DispatcherQueue.TryEnqueue(() => HandleActivation(activation));
        HandleActivation(AppInstance.GetCurrent().GetActivatedEventArgs());
    }

    private void HandleActivation(AppActivationArguments activation)
    {
        try
        {
            if (activation.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launchArgs)
            {
                var match = System.Text.RegularExpressions.Regex.Match(launchArgs.Arguments, @"--download-request\s+([a-f0-9]{32})");
                if (match.Success)
                {
                    var path = System.IO.Path.Combine(Services.CoreClient.DataDirectory, "pending-downloads", match.Groups[1].Value + ".json");
                    var request = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject();
                    System.IO.File.Delete(path);
                    OpenDownloadWindow(request); return;
                }
            }
            var link = activation.Data switch
            {
                Windows.ApplicationModel.Activation.IProtocolActivatedEventArgs protocol => protocol.Uri.AbsoluteUri,
                Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch => Services.GopeedLink.FromCommandLine(launch.Arguments),
                _ => null
            };
            if (link is null && activation.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs command && Services.DownloadSources.FromArguments(command.Arguments) is { } source)
            {
                OpenDownloadWindow(new System.Text.Json.Nodes.JsonObject { ["req"] = new System.Text.Json.Nodes.JsonObject { ["url"] = source } }); return;
            }
            if (link is not null && Services.GopeedLink.Parse(link) is { Route: "create" } create)
            {
                OpenDownloadWindow(create.Parameters ?? new System.Text.Json.Nodes.JsonObject()); return;
            }
            EnsureMainWindow().Activate();
            if (link is not null) ((MainWindow)Window).OpenProtocol(link);
        }
        catch (Exception error)
        {
            var main = EnsureMainWindow(); main.Activate(); main.ReportError(Strings.Get("Errors.OpenDownload") + error.Message);
        }
    }
    private MainWindow EnsureMainWindow()
    {
        if (Window is null) { Window = new MainWindow(); Window.Closed += (_, _) => { Window = null!; ReleaseRegistration(); }; }
        return (MainWindow)Window;
    }
    internal void OpenDownloadWindow(System.Text.Json.Nodes.JsonObject request, bool compact = true)
    {
        var window = new Views.DownloadWindow(request, compact);
        TrackDownloadWindow(window);
        Services.WindowActivation.ShowConfirmation(window);
    }
    internal void OpenProgressWindow(string taskId)
    {
        var window = downloadWindows.FirstOrDefault(window => window.TaskId == taskId);
        if (window is null) { window = new Views.DownloadWindow(taskId); TrackDownloadWindow(window); }
        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) presenter.Restore();
        window.AppWindow.Show(true); window.Activate();
    }
    private void TrackDownloadWindow(Views.DownloadWindow window)
    {
        downloadWindows.Add(window);
        window.Closed += (_, _) => { downloadWindows.Remove(window); ReleaseRegistration(); };
    }
    internal void ShowDeferredDownloads()
    {
        var main = EnsureMainWindow();
        main.Activate();
        main.ShowDeferredDownloads();
    }
    private void ReleaseRegistration()
    {
        if (Window is null && downloadWindows.Count == 0) registeredInstance?.UnregisterKey();
    }
}
