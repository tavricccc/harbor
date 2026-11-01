using Harbor.Glass;

namespace Harbor;

/// <summary>The download list's glass window.</summary>
public sealed class MainWindow
{
    private readonly MainPage page = new();
    internal GlassWindow Glass { get; }
    internal event EventHandler? Closed;

    public MainWindow()
    {
        page.RequestedTheme = Services.WindowAppearance.Theme;
        Glass = new GlassWindow(page, "Harbor", 1120, 720, resizable: true, minWidth: 780, minHeight: 520);
        Glass.SetCaption(page.TitleBar);
        page.MinimizeRequested += Glass.Minimize;
        page.CloseRequested += Glass.Close;
        Glass.Closed += (_, _) => { Services.ShareFiles.Release(Glass.Handle); Closed?.Invoke(this, EventArgs.Empty); };
        Glass.Activated += (_, _) => page.UpdatePollingVisibility();
        Glass.VisibilityChanged += (_, _) => page.UpdatePollingVisibility();
    }

    public void Activate() => Glass.Activate();
    public void OpenProtocol(string link) => page.OpenProtocol(link);
    public void ReportError(string message) => page.ViewModel.Error = message;
    public void ShowDeferredDownloads() => page.ShowDeferredDownloads();
}
