using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using Gopeed_Native.Views;

namespace Gopeed_Native;

public sealed partial class MainPage
{
    private void InitializeBrowserSetupHint()
    {
        NativeInfoBars.CollapseWhenClosed(BrowserSetupHint);
        Loaded += (_, _) => BrowserSetupHint.IsOpen = !UiPreferences.Load().BrowserSetupHintDismissed;
    }

    private void OpenBrowserSetup(object sender, RoutedEventArgs args)
    {
        BrowserSetupHint.IsOpen = false;
        OpenSection(new BrowserSetupPage());
    }

    private void BrowserSetupHintClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        var preferences = UiPreferences.Load();
        preferences.BrowserSetupHintDismissed = true;
        preferences.Save();
    }
}
