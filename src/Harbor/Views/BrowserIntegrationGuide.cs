using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;

namespace Harbor.Views;

internal sealed class BrowserIntegrationGuide : StackPanel
{
    private readonly TextBlock registration = SettingsFields.Description(Strings.Get("Browser.Checking"));
    private readonly Button enable = NativeButtons.Create(Strings.Get("Browser.Enable"), "\uE774");
    private readonly InfoBar message = new() { IsClosable = true };

    internal BrowserIntegrationGuide()
    {
        Spacing = 16;
        NativeInfoBars.CollapseWhenClosed(message);
        Children.Add(message);
        var install = Step(Strings.Get("Browser.InstallStep"),
            Strings.Get("Browser.InstallDescription"));
        foreach (var (browser, url) in BrowserExtensionLinks.Stores)
            install.Children.Add(new HyperlinkButton { Content = Strings.Format("Browser.InstallIn", browser), NavigateUri = new Uri(url), Padding = new Thickness(0) });
        install.Children.Add(new HyperlinkButton { Content = Strings.Get("Browser.Documentation"), NavigateUri = new Uri(BrowserExtensionLinks.Documentation), Padding = new Thickness(0) });
        Children.Add(install);

        var connect = Step(Strings.Get("Browser.ConnectStep"),
            Strings.Get("Browser.ConnectDescription"));
        connect.Children.Add(registration);
        connect.Children.Add(enable);
        enable.Click += Enable;
        Children.Add(connect);

        Children.Add(Step(Strings.Get("Browser.TestStep"),
            Strings.Get("Browser.TestDescription")));
        Children.Add(new Expander {
            Header = Strings.Get("Browser.Troubleshoot"), HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SettingsFields.Description(Strings.Get("Browser.TroubleshootDescription"))
        });
        Loaded += (_, _) => RefreshRegistration();
    }

    private static StackPanel Step(string title, string description)
    {
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["CompactSectionTitleStyle"] });
        section.Children.Add(SettingsFields.Description(description));
        return section;
    }

    private void RefreshRegistration()
    {
        try
        {
            var registered = WindowsIntegration.IsBrowserHostRegistered();
            registration.Text = registered ? Strings.Get("Browser.Registered") : Strings.Get("Browser.NotRegistered");
            enable.Content = registered ? Strings.Get("Browser.Reenable") : Strings.Get("Browser.Enable");
            enable.Visibility = registered ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception error) { Report(error); }
    }

    private void Enable(object sender, RoutedEventArgs args)
    {
        try
        {
            WindowsIntegration.InstallBrowserHost();
            RefreshRegistration();
            message.Severity = InfoBarSeverity.Success;
            message.Message = Strings.Get("Browser.EnabledMessage");
            message.IsOpen = true;
        }
        catch (Exception error) { Report(error); }
    }

    private void Report(Exception error)
    {
        message.Severity = InfoBarSeverity.Error;
        message.Message = UserError.Message(error);
        message.IsOpen = true;
    }
}
