using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

/// <summary>The browser capture page: title, what capture does, then <see cref="BrowserIntegrationGuide"/>.</summary>
internal sealed class BrowserSetupPage : Page
{
    internal BrowserSetupPage()
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(4, 8, 20, 24), MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = Strings.Get("Browser.SetupTitle"), Style = (Style)Application.Current.Resources["UiSectionTitle"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = Strings.Get("Browser.Intro"), Style = (Style)Application.Current.Resources["UiBody"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new BrowserIntegrationGuide());
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }
}
