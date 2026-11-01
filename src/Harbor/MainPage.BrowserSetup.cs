using Microsoft.UI.Xaml;
using Harbor.Services;
using Harbor.Views;

namespace Harbor;

public sealed partial class MainPage
{
    private void InitializeBrowserSetupHint()
    {
        Loaded += (_, _) => BrowserSetupHint.Visibility = UiPreferences.Load().BrowserSetupHintDismissed ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenBrowserSetup(object sender, RoutedEventArgs args)
    {
        DismissBrowserSetupHint();
        OpenSection(new BrowserSetupPage());
    }

    private void BrowserSetupHintClosed(object sender, RoutedEventArgs args) => DismissBrowserSetupHint();

    // Setting up or closing the hint both retire it for good.
    private void DismissBrowserSetupHint()
    {
        BrowserSetupHint.Visibility = Visibility.Collapsed;
        var preferences = UiPreferences.Load();
        preferences.BrowserSetupHintDismissed = true;
        preferences.Save();
    }
}
