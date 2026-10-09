using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;

namespace Harbor.Views;

internal sealed class BrowserIntegrationGuide : StackPanel
{
    private readonly Button enable = NativeButtons.Create(Strings.Get("Browser.Enable"), "\uE774");
    private readonly InfoBar message = new() { IsClosable = true };

    internal BrowserIntegrationGuide()
    {
        Spacing = 16;
        NativeInfoBars.CollapseWhenClosed(message);
        Children.Add(message);
        enable.Click += Enable;
        Children.Add(enable);
        var install = new StackPanel { Spacing = 8 };
        foreach (var (browser, url) in BrowserExtensionLinks.Stores)
            install.Children.Add(new HyperlinkButton { Content = Strings.Format("Browser.InstallIn", browser), NavigateUri = new Uri(url), Padding = new Thickness(0) });
        install.Children.Add(new HyperlinkButton { Content = Strings.Get("Browser.Documentation"), NavigateUri = new Uri(BrowserExtensionLinks.Documentation), Padding = new Thickness(0) });
        Children.Add(install);

        Loaded += (_, _) => RefreshRegistration();
    }

    private void RefreshRegistration()
    {
        try
        {
            var registered = WindowsIntegration.IsBrowserHostRegistered();
            enable.Content = registered ? Strings.Get("Browser.Reenable") : Strings.Get("Browser.Enable");
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
