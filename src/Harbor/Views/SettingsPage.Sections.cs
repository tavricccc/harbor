using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class SettingsPage
{
    private void BuildNetwork()
    {
        var panel = Section(Strings.Get("Settings.Network"));
        panel.Children.Add(proxyMode);
        var customProxy = new StackPanel { Spacing = 16 };
        panel.Children.Add(customProxy);
        void ShowProxy() => customProxy.Visibility = proxyMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        proxyMode.SelectionChanged += (_, _) => ShowProxy(); ShowProxy();
        var (scheme, host) = SettingsFields.Columns(customProxy);
        fields.Text(scheme, Strings.Get("Common.Protocol"), "proxy.scheme", initial: "http");
        fields.Text(host, Strings.Get("Proxy.Host"), "proxy.host");
        var (user, password) = SettingsFields.Columns(customProxy);
        fields.Text(user, Strings.Get("Common.Username"), "proxy.usr");
        fields.Password(password, Strings.Get("Common.Password"), "proxy.pwd");

        var github = SettingsFields.Advanced(panel, Strings.Get("Settings.Mirrors")); github.Children.Add(mirrors);
        var browser = SettingsFields.Group(panel, Strings.Get("Settings.Browser"));
        browser.Children.Add(new BrowserIntegrationGuide());
        var remote = SettingsFields.Advanced(panel, Strings.Get("Settings.LocalApi"));
        remote.Children.Add(new TextBox { Header = Strings.Get("Settings.ServerAddress"), IsReadOnly = true, Text = new Uri(vm.Core.ApiAddress).Authority });
        remote.Children.Add(new PasswordBox { Header = "API Token", Password = vm.Core.Token, PasswordRevealMode = PasswordRevealMode.Peek });
        var copy = NativeButtons.Create(Strings.Get("Settings.CopyToken"), "\uE8C8");
        copy.Click += (_, _) => { FileActions.Copy(vm.Core.Token); Success(Strings.Get("Settings.TokenCopied")); };
        remote.Children.Add(copy); remote.Children.Add(apiPort);
    }

    private void BuildAutomation()
    {
        var panel = Section(Strings.Get("Settings.Automation"));
        var webhookEnabled = fields.Toggle(panel, Strings.Get("Settings.Webhook"), "webhook.enable");
        var webhook = new StackPanel { Spacing = 16 }; panel.Children.Add(webhook);
        SettingsFields.Reveal(webhookEnabled, webhook);
        var urls = fields.Text(webhook, Strings.Get("Settings.WebhookUrls"), "webhook.urls", true);
        var test = NativeButtons.Create(Strings.Get("Settings.TestWebhook"), "\uE724");
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            try
            {
                var targets = ConfigJson.Lines(urls.Text); if (targets.Length == 0) throw new FormatException(Strings.Get("Errors.WebhookRequired"));
                foreach (var url in targets)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new FormatException(Strings.Get("Errors.WebhookUrl"));
                    await vm.Core.SendAsync(HttpMethod.Post, "webhook/test", new JsonObject { ["url"] = url });
                }
                Success(Strings.Get("Settings.WebhookSent"));
            }
            catch (Exception error) { Report(error); }
            finally { test.IsEnabled = true; }
        };
        webhook.Children.Add(test);
        var script = SettingsFields.Group(panel, Strings.Get("Settings.Scripts"));
        var scriptEnabled = fields.Toggle(script, Strings.Get("Settings.RunScript"), "script.enable");
        var scriptPaths = new StackPanel { Spacing = 16 }; script.Children.Add(scriptPaths);
        SettingsFields.Reveal(scriptEnabled, scriptPaths);
        fields.Text(scriptPaths, Strings.Get("Settings.ScriptPaths"), "script.paths", true);
    }

    private void BuildAbout()
    {
        var panel = Section(Strings.Get("Settings.About"));
        panel.Children.Add(new TextBlock { Text = $"Harbor {typeof(App).Assembly.GetName().Version?.ToString(3)}", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        panel.Children.Add(SettingsFields.Description(Strings.Format("Settings.CoreVersion", vm.Core.Version)));
        var credits = SettingsFields.Group(panel, Strings.Get("Settings.Credits"));
        credits.Children.Add(new HyperlinkButton { Content = "Gopeed · GopeedLab", NavigateUri = new Uri("https://github.com/GopeedLab/gopeed"), Padding = new Thickness(0) });
        panel.Children.Add(checkUpdates);
        var update = NativeButtons.Create(Strings.Get("Update.Check"), "\uE72C");
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try { var release = await UpdateService.CheckAsync(vm.Core); if (release is null) Success(Strings.Get("Update.UpToDate")); else await UpdateService.PromptAsync(vm.Core, release, XamlRoot); }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        panel.Children.Add(update);
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        links.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.Project"), NavigateUri = new Uri("https://github.com/tavricccc/harbor"), Padding = new Thickness(0) });
        links.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.License"), NavigateUri = new Uri("https://github.com/tavricccc/harbor/blob/winui-native/LICENSE"), Padding = new Thickness(0) });
        panel.Children.Add(links);
        var logs = NativeButtons.Create(Strings.Get("Settings.OpenLogs"), "\uE8B7");
        logs.Click += (_, _) => FileActions.Open(Path.Combine(CoreClient.DataDirectory, "logs")); panel.Children.Add(logs);
        var stop = NativeButtons.Create(Strings.Get("Settings.Quit"), "\uE7E8");
        stop.Click += async (_, _) =>
        {
            var dialog = new ContentDialog { Title = Strings.Get("Settings.QuitConfirm"), Content = Strings.Get("Settings.QuitDescription"), PrimaryButtonText = Strings.Get("Settings.QuitAction"), CloseButtonText = Strings.Get("Common.Cancel") };
            if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
            {
                try { await vm.Core.StopAsync(); Application.Current.Exit(); }
                catch (Exception error) { Report(error); }
            }
        };
        panel.Children.Add(stop);
    }
}
