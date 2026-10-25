using System.Runtime.InteropServices;
using Harbor.Localization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace HarborSetup;

/// One compact card in Harbor's style: what will happen, then a single primary action.
internal sealed class SetupWindow : Window
{
    private const double CardWidth = 520;

    private readonly bool uninstall;
    private readonly Grid root = new() { Padding = new Thickness(28, 20, 28, 24), RowSpacing = 12 };
    private readonly TextBlock title = new() { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock version = new() { FontSize = 12 };
    private readonly TextBlock detail = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 20 };
    private readonly StackPanel options = new() { Spacing = 4 };
    private readonly CheckBox desktop = new() { Content = Strings.Get("Installer.DesktopIcon"), IsChecked = Installer.HasDesktopShortcut };
    private readonly ProgressBar progress = new() { IsIndeterminate = true, Visibility = Visibility.Collapsed };
    private readonly StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
    private bool removed;

    public SetupWindow(bool uninstall)
    {
        this.uninstall = uninstall;
        Title = Strings.Get(uninstall ? "Installer.UninstallTitle" : "Installer.Title");
        root.Language = Strings.Language;
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        heading.Children.Add(new Image { Width = 40, Height = 40, Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Harbor.png"))) });
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        title.FontFamily = new FontFamily("Segoe UI Variable Display, Microsoft JhengHei UI");
        version.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        version.Text = Strings.Format("Installer.Version", Installer.Version);
        names.Children.Add(title);
        names.Children.Add(version);
        heading.Children.Add(names);
        root.Children.Add(heading);

        detail.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        Grid.SetRow(detail, 1);
        root.Children.Add(detail);

        if (!uninstall)
        {
            options.Children.Add(desktop);
            var license = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            license.Children.Add(new TextBlock { Text = Strings.Get("Installer.LicenseAgreement"), VerticalAlignment = VerticalAlignment.Center, Foreground = detail.Foreground });
            var view = new HyperlinkButton { Content = Strings.Get("Installer.ViewLicense"), Padding = new Thickness(4, 2, 4, 2) };
            view.Click += async (_, _) => await ShowLicense();
            license.Children.Add(view);
            options.Children.Add(license);
        }
        Grid.SetRow(options, 2);
        root.Children.Add(options);
        Grid.SetRow(progress, 3);
        root.Children.Add(progress);
        buttons.VerticalAlignment = VerticalAlignment.Bottom;
        buttons.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(buttons, 4);
        root.Children.Add(buttons);
        Content = root;

        ConfigureWindow();
        root.Loaded += (_, _) => FitToContent();
        Closed += (_, _) =>
        {
            if (removed) Installer.ScheduleSelfCleanup();
        };
        ShowIntro();
    }

    private void ConfigureWindow()
    {
        SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var corners = 2;
        DwmSetWindowAttribute(hwnd, 33, ref corners, sizeof(int));
        root.ActualThemeChanged += (_, _) => SyncTitleBar(hwnd);
        SyncTitleBar(hwnd);

        FitToContent();
    }

    /// Sizes the window to the intro's content, so nothing is cut off in any language,
    /// and keeps that size for the shorter states that follow.
    private void FitToContent()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        root.Measure(new Windows.Foundation.Size(CardWidth, double.PositiveInfinity));
        var height = Math.Max(root.DesiredSize.Height, 200);
        AppWindow.ResizeClient(new SizeInt32((int)Math.Round(CardWidth * scale), (int)Math.Round(height * scale)));
        var size = AppWindow.Size;
        var work = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Move(new PointInt32(work.X + (work.Width - size.Width) / 2, work.Y + (work.Height - size.Height) / 2));
    }

    private void SyncTitleBar(nint hwnd)
    {
        var dark = !new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast && root.ActualTheme == ElementTheme.Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    private void ShowIntro()
    {
        if (uninstall)
            Show(Strings.Get("Installer.UninstallTitle"), Strings.Get("Installer.UninstallDetail"),
                (Strings.Get("Common.Cancel"), false, Close), (Strings.Get("Installer.Uninstall"), true, Run));
        else if (Installer.IsInstalled)
            Show(Strings.Get("Installer.UpgradeHeading"), Strings.Format("Installer.UpgradeDetail", Installer.InstallDir),
                (Strings.Get("Common.Cancel"), false, Close), (Strings.Get("Installer.Update"), true, Run));
        else
            Show(Strings.Get("Installer.InstallHeading"), Strings.Format("Installer.InstallDetail", Installer.InstallDir),
                (Strings.Get("Common.Cancel"), false, Close), (Strings.Get("Installer.Install"), true, Run));
    }

    private async void Run()
    {
        var createDesktopShortcut = desktop.IsChecked == true;
        options.Visibility = Visibility.Collapsed;
        progress.Visibility = Visibility.Visible;
        Show(Strings.Get(uninstall ? "Installer.Uninstalling" : "Installer.Installing"),
            Strings.Get(uninstall ? "Installer.UninstallingDetail" : "Installer.InstallingDetail"));
        try
        {
            await Task.Run(() =>
            {
                if (uninstall) Installer.Uninstall();
                else Installer.Install(createDesktopShortcut);
            });
            progress.Visibility = Visibility.Collapsed;
            if (uninstall)
            {
                removed = true;
                Show(Strings.Get("Installer.Removed"), Strings.Get("Installer.RemovedDetail"), (Strings.Get("Common.Close"), true, Close));
            }
            else
                Show(Strings.Get("Installer.Ready"), Strings.Get("Installer.ReadyDetail"),
                    (Strings.Get("Common.Close"), false, Close), (Strings.Get("Installer.LaunchHarbor"), true, () => { Installer.Launch(); Close(); }));
        }
        catch (Exception error)
        {
            progress.Visibility = Visibility.Collapsed;
            Show(Strings.Get(uninstall ? "Installer.UninstallFailed" : "Installer.InstallFailed"), error.Message, (Strings.Get("Common.Close"), true, Close));
        }
    }

    private void Show(string heading, string body, params (string Label, bool Primary, Action Action)[] actions)
    {
        title.Text = heading;
        detail.Text = body;
        buttons.Children.Clear();
        foreach (var (label, primary, action) in actions)
        {
            var button = new Button { Content = label, MinWidth = 96 };
            if (primary)
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            button.Click += (_, _) => action();
            buttons.Children.Add(button);
        }
    }

    private async Task ShowLicense()
    {
        var text = new TextBlock { Text = await Task.Run(() => Installer.License), TextWrapping = TextWrapping.Wrap, FontSize = 12, IsTextSelectionEnabled = true };
        var dialog = new ContentDialog
        {
            XamlRoot = root.XamlRoot,
            Title = Strings.Get("Installer.ViewLicense"),
            Content = new ScrollViewer { Content = text, MaxHeight = 360 },
            CloseButtonText = Strings.Get("Common.Close"),
            RequestedTheme = root.ActualTheme,
        };
        await dialog.ShowAsync();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
