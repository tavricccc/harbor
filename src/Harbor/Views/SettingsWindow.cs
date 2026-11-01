using Harbor.Glass;
using Harbor.Localization;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.ViewModels;

namespace Harbor.Views;

public sealed class SettingsWindow
{
    internal GlassWindow Glass { get; }
    public event EventHandler? Closed;
    private readonly SettingsPage page;

    public SettingsWindow(DownloadsViewModel viewModel)
    {
        var root = new UserControl { RequestedTheme = WindowAppearance.Theme, IsTabStop = false };
        Glass = new GlassWindow(root, Strings.Get("Settings.WindowTitle"), 860, 680, resizable: true, minWidth: 720, minHeight: 520);
        page = new SettingsPage(viewModel, Glass.Handle);
        page.CloseRequested += Glass.Close;
        page.MinimizeRequested += Glass.Minimize;
        root.Content = page;
        Glass.SetCaption(page.TitleBar);
        Glass.Closed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    public void Activate() => Glass.Activate();
    public void ShowAbout() => page.ShowAbout();
    public void Close() => Glass.Close();
}
