using Microsoft.UI.Xaml;

namespace Harbor;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Closed += (_, _) => Services.ShareFiles.Release(hwnd);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1120 * scale), (int)(720 * scale)));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(780 * scale);
            presenter.PreferredMinimumHeight = (int)(520 * scale);
        }
        ((FrameworkElement)Content).RequestedTheme = Services.WindowAppearance.Theme;
        Services.WindowAppearance.ApplyFrame(this, (FrameworkElement)Content);

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
        Activated += (_, _) => ((MainPage)RootFrame.Content).UpdatePollingVisibility();
        AppWindow.Changed += (_, args) => { if (args.DidVisibilityChange || args.DidPresenterChange) ((MainPage)RootFrame.Content).UpdatePollingVisibility(); };
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
    public void OpenProtocol(string link) => ((MainPage)RootFrame.Content).OpenProtocol(link);
    public void ReportError(string message) => ((MainPage)RootFrame.Content).ViewModel.Error = message;
    public void ShowDeferredDownloads() => ((MainPage)RootFrame.Content).ShowDeferredDownloads();
}
