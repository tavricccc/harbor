using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;

namespace Harbor.Services;

public static class WindowAppearance
{
    public static ElementTheme Theme
    {
        get
        {
            var path = Path.Combine(CoreClient.DataDirectory, "theme.txt");
            return File.Exists(path) ? File.ReadAllText(path).Trim() switch { "1" => ElementTheme.Light, "2" => ElementTheme.Dark, _ => ElementTheme.Default } : ElementTheme.Default;
        }
    }
    public static void SetIcon(Window window) => window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

    public static void ApplyFrame(Window window, FrameworkElement root)
    {
        SetIcon(window);
        window.SystemBackdrop = new MicaBackdrop();
        var corners = 2;
        DwmSetWindowAttribute(WinRT.Interop.WindowNative.GetWindowHandle(window), 33, ref corners, sizeof(int));
        root.ActualThemeChanged += (_, _) => SyncTitleBar(window, root);
        SyncTitleBar(window, root);
    }

    private static void SyncTitleBar(Window window, FrameworkElement root)
    {
        var dark = !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast
            && root.ActualTheme == ElementTheme.Dark ? 1 : 0;
        DwmSetWindowAttribute(WinRT.Interop.WindowNative.GetWindowHandle(window), 20, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
