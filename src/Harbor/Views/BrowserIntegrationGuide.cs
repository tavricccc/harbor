using Harbor.Controls;
using Harbor.Localization;
using Harbor.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Harbor.Views;

/// <summary>
/// Browser download capture: a status card (on or not, with the one action) above numbered steps — enable the local
/// host, then install the Gopeed extension in a browser — and the extension guide link. Shown on its own page and in
/// Settings → Network.
/// </summary>
internal sealed class BrowserIntegrationGuide : StackPanel
{
    private readonly TextBlock statusTitle = new() { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock statusNote = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Icon statusIcon = new() { Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border statusBadge = new() { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button action = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel steps = new();
    private string? error;

    internal BrowserIntegrationGuide()
    {
        Spacing = 16;
        statusBadge.Child = statusIcon;
        statusNote.Style = (Style)Application.Current.Resources["UiNote"];
        Motion.Themed(statusTitle, TextBlock.ForegroundProperty, "UiText1Brush");
        action.Click += Enable;

        var status = new Grid { ColumnSpacing = 14 };
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        status.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { statusTitle, statusNote } };
        status.Children.Add(statusBadge);
        Grid.SetColumn(text, 1); status.Children.Add(text);
        Grid.SetColumn(action, 2); status.Children.Add(action);
        Children.Add(Card(status, new Thickness(16, 14, 16, 14)));

        Children.Add(new TextBlock { Text = Strings.Get("Browser.Steps"), Style = (Style)Application.Current.Resources["UiGroupTitle"] });
        Children.Add(Card(steps, new Thickness(0)));
        Children.Add(new HyperlinkButton { Content = Strings.Get("Browser.Documentation"), NavigateUri = new Uri(BrowserExtensionLinks.Documentation), Padding = new Thickness(0) });
        Loaded += (_, _) => Render();
    }

    private void Enable(object sender, RoutedEventArgs args)
    {
        try { WindowsIntegration.InstallBrowserHost(); error = null; }
        catch (Exception failure) { error = UserError.Message(failure); }
        Render();
    }

    private void Render()
    {
        bool on;
        try { on = WindowsIntegration.IsBrowserHostRegistered(); }
        catch (Exception failure) { on = false; error ??= UserError.Message(failure); }
        statusTitle.Text = Strings.Get(on ? "Browser.StatusOn" : "Browser.StatusOff");
        statusNote.Text = error ?? Strings.Get(on ? "Browser.StatusOnNote" : "Browser.StatusOffNote");
        statusNote.Foreground = Motion.Resource(statusNote, error is null ? "UiText3Brush" : "UiDangerBrush");
        statusIcon.Glyph = on ? "check" : "link";
        statusBadge.Background = on ? Motion.Accent : Motion.Resource(statusBadge, "UiFillSelectedBrush");
        statusIcon.Foreground = on ? new SolidColorBrush(Microsoft.UI.Colors.White) : Motion.Resource(statusIcon, "UiText1Brush");
        // The page's one accent button: enable while capture is off; once on, a quiet repair.
        NativeButtons.SetContent(action, Strings.Get(on ? "Browser.Reenable" : "Browser.Enable"));
        action.Style = (Style)Application.Current.Resources[on ? "UiTextButton" : "UiAccentButton"];

        steps.Children.Clear();
        steps.Children.Add(Step(1, Strings.Get("Browser.Enable"), Strings.Get("Browser.StepEnableNote"), on, null));
        int number = 2;
        foreach (var (browser, url) in BrowserExtensionLinks.Stores)
        {
            var open = new HyperlinkButton { Content = Strings.Get("Browser.OpenStore"), NavigateUri = new Uri(url) };
            steps.Children.Add(Step(number++, Strings.Format("Browser.InstallIn", browser), Strings.Get("Browser.StepInstallNote"), false, open));
        }
    }

    /// <summary>A numbered row: a 24 circle (the number, or an accent check when done), label and note, an optional link.</summary>
    private static Grid Step(int number, string label, string note, bool done, FrameworkElement? end)
    {
        var mark = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) };
        if (done)
        {
            mark.Background = Motion.Accent;
            mark.Child = new Icon { Glyph = "check", Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
        }
        else
        {
            mark.BorderThickness = new Thickness(1);
            Motion.Themed(mark, Border.BorderBrushProperty, "UiStrokeBrush");
            var digit = new TextBlock { Text = number.ToString(), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Motion.Themed(digit, TextBlock.ForegroundProperty, "UiText2Brush");
            mark.Child = digit;
        }
        var title = new TextBlock { Text = label, FontSize = 14, TextWrapping = TextWrapping.Wrap };
        Motion.Themed(title, TextBlock.ForegroundProperty, "UiText1Brush");
        var text = new StackPanel { Spacing = 2, Children = { title, new TextBlock { Text = note, Style = (Style)Application.Current.Resources["UiNote"], TextWrapping = TextWrapping.Wrap } } };
        var row = new Grid { ColumnSpacing = 12, Padding = new Thickness(14, 12, 14, 12), MinHeight = 56 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(mark);
        Grid.SetColumn(text, 1); row.Children.Add(text);
        if (end is not null) { end.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(end, 2); row.Children.Add(end); }
        // Rows after the first get the 1 px divider, as in a settings card.
        if (number > 1) { row.BorderThickness = new Thickness(0, 1, 0, 0); Motion.Themed(row, Grid.BorderBrushProperty, "UiStrokeBrush"); }
        return row;
    }

    internal static Border Card(UIElement child, Thickness padding)
    {
        var card = new Border { Padding = padding, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Child = child };
        Motion.Themed(card, Border.BackgroundProperty, "UiFillControlBrush");
        Motion.Themed(card, Border.BorderBrushProperty, "UiStrokeBrush");
        return card;
    }
}
