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
        foreach (var (tag, name) in new[] { ("", Strings.Get("Common.FollowWindows")), ("zh-TW", "繁體中文"), ("en-US", "English"), ("zh-CN", "简体中文") })
            selector.Items.Add(new ComboBoxItem { Content = name, Tag = tag });
        selector.SelectedIndex = 0;
        AutomationProperties.SetAutomationId(selector, "InterfaceLanguage");
        AutomationProperties.SetName(selector, Strings.Get("Settings.Language"));
        return selector;
    }
}
