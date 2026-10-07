using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

internal static class NativeButtons
{
    public static Button Create(string label, string glyph, bool primary = false)
    {
        var button = new Button();
        if (primary) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        SetContent(button, label, glyph); return button;
    }
    public static void SetContent(Button button, string label, string glyph)
    {
        if (button.Tag?.ToString() == label) return;
        button.Tag = label;
        button.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new FontIcon { Glyph = glyph, FontSize = 16 }, new TextBlock { Text = label } } };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);
    }
}
