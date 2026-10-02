using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Gopeed_Native.Views;

internal static class NativeInfoBars
{
    public static void CollapseWhenClosed(InfoBar bar)
    {
        bar.Visibility = bar.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        bar.RegisterPropertyChangedCallback(InfoBar.IsOpenProperty, (sender, _) =>
        {
            var current = (InfoBar)sender;
            current.Visibility = current.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        });
    }
}
