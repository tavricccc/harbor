using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

/// <summary>Text buttons for code-built views; the primary one is the accent button.</summary>
internal static class NativeButtons
{
    public static Button Create(string label, bool primary = false)
    {
        var button = new Button();
        if (primary)
            button.Style = (Style)Application.Current.Resources["UiPrimaryButton"];
        SetContent(button, label);
        return button;
    }
    public static void SetContent(Button button, string label)
    {
        button.Content = label;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label);
    }
}
