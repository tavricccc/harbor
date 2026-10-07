using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;

namespace Harbor.Views;

internal sealed class BrowserIntegrationGuide : StackPanel
{
    private readonly TextBlock registration = SettingsFields.Description("正在檢查 Harbor 的本機接管註冊…");
    private readonly Button enable = NativeButtons.Create("啟用本機接管", "\uE774");
    private readonly InfoBar message = new() { IsClosable = true };

    internal BrowserIntegrationGuide()
    {
        Spacing = 16;
        NativeInfoBars.CollapseWhenClosed(message);
        Children.Add(message);
        var install = Step("1. 安裝 Gopeed 官方擴充套件",
            "擴充套件需在瀏覽器另行安裝。請用要接管下載的瀏覽器，開啟對應商店並加入 Gopeed。");
        foreach (var (browser, url) in BrowserExtensionLinks.Stores)
            install.Children.Add(new HyperlinkButton { Content = $"在 {browser} 安裝 Gopeed", NavigateUri = new Uri(url), Padding = new Thickness(0) });
        install.Children.Add(new HyperlinkButton { Content = "官方擴充套件說明", NavigateUri = new Uri(BrowserExtensionLinks.Documentation), Padding = new Thickness(0) });
        Children.Add(install);

        var connect = Step("2. Harbor 自動連接瀏覽器",
            "安裝時會自動註冊本機接管，啟動 Harbor 時也會補上缺少的註冊，不需手動啟用。");
        connect.Children.Add(registration);
        connect.Children.Add(enable);
        enable.Click += Enable;
        Children.Add(connect);

        Children.Add(Step("3. 關閉遠端下載並測試",
            "在 Gopeed 擴充套件設定中關閉「遠端下載」，並確認擴充套件已啟用。本機接管不用填伺服器位址或 Token。回到網站嘗試下載檔案，出現 Harbor 確認視窗就表示接管生效。"));
        Children.Add(new Expander {
            Header = "接管沒有反應？", HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = SettingsFields.Description("確認瀏覽器已安裝並啟用 Gopeed 擴充套件，且「遠端下載」已關閉。重新開啟 Harbor 可自動補上註冊，再重新開啟瀏覽器測試。")
        });
        Loaded += (_, _) => RefreshRegistration();
    }

    private static StackPanel Step(string title, string description)
    {
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, Style = (Style)Application.Current.Resources["CompactSectionTitleStyle"] });
        section.Children.Add(SettingsFields.Description(description));
        return section;
    }

    private void RefreshRegistration()
    {
        try
        {
            var registered = WindowsIntegration.IsBrowserHostRegistered();
            registration.Text = registered ? "Harbor 的本機接管已註冊。" : "目前未註冊到這份 Harbor。";
            enable.Content = registered ? "重新啟用本機接管" : "啟用本機接管";
            enable.Visibility = registered ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception error) { Report(error); }
    }

    private void Enable(object sender, RoutedEventArgs args)
    {
        try
        {
            WindowsIntegration.InstallBrowserHost();
            RefreshRegistration();
            message.Severity = InfoBarSeverity.Success;
            message.Message = "Harbor 本機接管已啟用。請在瀏覽器確認擴充套件已安裝，並關閉「遠端下載」。";
            message.IsOpen = true;
        }
        catch (Exception error) { Report(error); }
    }

    private void Report(Exception error)
    {
        message.Severity = InfoBarSeverity.Error;
        message.Message = UserError.Message(error);
        message.IsOpen = true;
    }
}
