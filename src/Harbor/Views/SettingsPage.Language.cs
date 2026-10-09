using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

public sealed partial class SettingsPage
{
    private readonly ComboBox language = CreateLanguageSelector();

    private static ComboBox CreateLanguageSelector()
    {
        var selector = new ComboBox
        {
            Header = Strings.Get("Settings.Language"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedValuePath = "Tag"
        };
        selector.Items.Add(new ComboBoxItem { Content = Strings.Get("Common.FollowWindows"), Tag = "" });
        foreach (var option in Strings.Languages)
            selector.Items.Add(new ComboBoxItem { Content = option.Name, Tag = option.Id });
        selector.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(selector, "InterfaceLanguage");
        AutomationProperties.SetName(selector, Strings.Get("Settings.Language"));
        return selector;
    }
}
