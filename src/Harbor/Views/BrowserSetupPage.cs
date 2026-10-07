using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

internal sealed class BrowserSetupPage : Page
{
    internal BrowserSetupPage()
    {
        var panel = new StackPanel { Spacing = 20, Margin = new Thickness(4, 8, 20, 24), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = "設定瀏覽器下載接管", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new BrowserIntegrationGuide());
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
}
