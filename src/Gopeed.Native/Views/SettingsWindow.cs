using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using Gopeed_Native.ViewModels;

namespace Gopeed_Native.Views;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(DownloadsViewModel viewModel)
    {
        Title = "Harbor 設定";
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var root = new Grid { RequestedTheme = WindowAppearance.Theme };
        var page = new SettingsPage(viewModel, handle);
        page.CloseRequested += Close;
        root.Children.Add(page); Content = root;
        WindowAppearance.ApplyFrame(this, root);

        var scale = GetDpiForWindow(handle) / 96.0;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(Math.Min((int)(820 * scale), area.Width - 32), Math.Min((int)(760 * scale), area.Height - 64)));
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(720 * scale);
            presenter.PreferredMinimumHeight = (int)(560 * scale);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
