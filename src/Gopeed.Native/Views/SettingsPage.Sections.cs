using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Gopeed_Native.Views;

public sealed partial class SettingsPage
{
    private void BuildNetwork()
    {
        var panel = Section("連線");
        panel.Children.Add(proxyMode);
        var (scheme, host) = SettingsFields.Columns(panel);
        fields.Text(scheme, "協定", "proxy.scheme", initial: "http");
        fields.Text(host, "主機與連接埠", "proxy.host");
        var (user, password) = SettingsFields.Columns(panel);
        fields.Text(user, "使用者名稱", "proxy.usr");
        fields.Password(password, "密碼", "proxy.pwd");

        var github = SettingsFields.Group(panel, "GitHub 鏡像"); github.Children.Add(mirrors);
        var browser = SettingsFields.Group(panel, "官方瀏覽器擴充功能");
        browser.Children.Add(new BrowserIntegrationGuide());
        var remote = SettingsFields.Group(panel, "遠端下載連線");
        remote.Children.Add(SettingsFields.Description("在官方擴充功能選擇 HTTP，填入以下伺服器位址與 Token。"));
        remote.Children.Add(new TextBox { Header = "伺服器位址", IsReadOnly = true, Text = new Uri(vm.Core.ApiAddress).Authority });
        remote.Children.Add(new PasswordBox { Header = "API Token", Password = vm.Core.Token, PasswordRevealMode = PasswordRevealMode.Peek });
        var copy = NativeButtons.Create("複製 API Token", "\uE8C8");
        copy.Click += (_, _) => { FileActions.Copy(vm.Core.Token); Success("Token 已複製。"); };
        remote.Children.Add(copy); remote.Children.Add(apiPort);
    }

    private void BuildAutomation()
    {
        var panel = Section("自動化");
        fields.Toggle(panel, "完成下載後傳送 Webhook 通知", "webhook.enable");
        var urls = fields.Text(panel, "Webhook 網址（每行一個）", "webhook.urls", true);
        var test = NativeButtons.Create("測試 Webhook", "\uE724");
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            try
            {
                var targets = ConfigJson.Lines(urls.Text); if (targets.Length == 0) throw new FormatException("請先輸入 Webhook 網址。");
                foreach (var url in targets)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new FormatException("請輸入有效的 Webhook 網址。");
                    await vm.Core.SendAsync(HttpMethod.Post, "webhook/test", new JsonObject { ["url"] = url });
                }
                Success("測試通知已傳送。");
            }
            catch (Exception error) { Report(error); }
            finally { test.IsEnabled = true; }
        };
        panel.Children.Add(test);
        var script = SettingsFields.Group(panel, "執行程式");
        fields.Toggle(script, "完成或失敗後執行程式", "script.enable");
        fields.Text(script, "程式或指令檔路徑（每行一個）", "script.paths", true);
    }

    private void BuildAbout()
    {
        var panel = Section("關於");
        panel.Children.Add(new TextBlock { Text = $"Harbor {typeof(App).Assembly.GetName().Version?.ToString(3)}", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        var credits = SettingsFields.Group(panel, "致謝");
        credits.Children.Add(SettingsFields.Description("Harbor fork 自 Gopeed，沿用其開源下載引擎，並以 WinUI 3 製作 Windows 原生介面。感謝 GopeedLab 與所有 Gopeed 貢獻者提供的基礎與持續維護。"));
        credits.Children.Add(new HyperlinkButton { Content = "Gopeed · GopeedLab", NavigateUri = new Uri("https://github.com/GopeedLab/gopeed"), Padding = new Thickness(0) });
        credits.Children.Add(SettingsFields.Description("Harbor 是社群維護的獨立專案，並非 Gopeed 官方發行版。依 GPL-3.0 授權發布。"));
        panel.Children.Add(credits);
        panel.Children.Add(checkUpdates);
        var update = NativeButtons.Create("檢查更新", "\uE72C");
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try { var release = await UpdateService.CheckAsync(vm.Core); if (release is null) Success("已是最新版本。"); else await UpdateService.PromptAsync(vm.Core, release, XamlRoot); }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        panel.Children.Add(update);
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        links.Children.Add(new HyperlinkButton { Content = "專案首頁", NavigateUri = new Uri("https://github.com/tavricccc/harbor"), Padding = new Thickness(0) });
        links.Children.Add(new HyperlinkButton { Content = "GPL-3.0 授權", NavigateUri = new Uri("https://github.com/tavricccc/harbor/blob/winui-native/LICENSE"), Padding = new Thickness(0) });
        panel.Children.Add(links);
        var logs = NativeButtons.Create("開啟記錄資料夾", "\uE8B7");
        logs.Click += (_, _) => FileActions.Open(Path.Combine(CoreClient.DataDirectory, "logs")); panel.Children.Add(logs);
        var stop = NativeButtons.Create("結束程式並停止下載", "\uE7E8");
        stop.Click += async (_, _) =>
        {
            var dialog = new ContentDialog { Title = "停止所有下載並結束？", Content = "下載進度會保留，下次開啟可繼續。", PrimaryButtonText = "停止並結束", CloseButtonText = "取消" };
            if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
            {
                try { await vm.Core.StopAsync(); Application.Current.Exit(); }
                catch (Exception error) { Report(error); }
            }
        };
        panel.Children.Add(stop);
    }
}
