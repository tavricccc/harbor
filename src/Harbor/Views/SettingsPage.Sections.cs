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
        var page = Section(Strings.Get("Settings.Network"), "globe");
        var proxy = SettingsFields.Group(page, Strings.Get("Proxy.Title"));
        SettingsFields.Row(proxy, Strings.Get("Proxy.Mode"), proxyMode);
        var before = proxy.Children.Count;
        fields.Text(proxy, Strings.Get("Common.Protocol"), "proxy.scheme", initial: "http");
        fields.Text(proxy, Strings.Get("Proxy.Host"), "proxy.host");
        fields.Text(proxy, Strings.Get("Common.Username"), "proxy.usr");
        fields.Password(proxy, Strings.Get("Common.Password"), "proxy.pwd");
        var custom = proxy.Children.Skip(before).ToArray();
        void ShowProxy()
        {
            foreach (var row in custom)
                row.Visibility = proxyMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }
        proxyMode.SelectionChanged += (_, _) => ShowProxy();
        ShowProxy();

        var github = SettingsFields.Group(page, Strings.Get("Settings.Mirrors"));
        github.Children.Add(mirrors.Enabled);
        SettingsFields.Reveal(mirrors.Enabled, SettingsFields.Block(github, mirrors));

        var browser = SettingsFields.Group(page, Strings.Get("Settings.Browser"));
        SettingsFields.Block(browser, new BrowserIntegrationGuide());

        var remote = SettingsFields.Group(page, Strings.Get("Settings.LocalApi"));
        SettingsFields.Row(remote, Strings.Get("Settings.ServerAddress"), new TextBox { Width = 280, IsReadOnly = true, Text = new Uri(vm.Core.ApiAddress).Authority });
        var token = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        token.Children.Add(new PasswordBox { Width = 200, Password = vm.Core.Token, PasswordRevealMode = PasswordRevealMode.Peek });
        var copy = NativeButtons.Create(Strings.Get("Action.Copy"));
        copy.Click += (_, _) => { FileActions.Copy(vm.Core.Token); Success(Strings.Get("Settings.TokenCopied")); };
        token.Children.Add(copy);
        SettingsFields.Row(remote, "API Token", token);
        SettingsFields.Row(remote, Strings.Get("Settings.ApiPort"), apiPort);
    }

    private void BuildAutomation()
    {
        var page = Section(Strings.Get("Settings.Automation"), "link");
        var webhook = SettingsFields.Group(page, "Webhook");
        var webhookEnabled = fields.Toggle(webhook, Strings.Get("Settings.Webhook"), "webhook.enable");
        var urls = fields.Text(webhook, Strings.Get("Settings.WebhookUrls"), "webhook.urls", true);
        var test = NativeButtons.Create(Strings.Get("Action.SendTest"));
        test.Click += async (_, _) =>
        {
            test.IsEnabled = false;
            try
            {
                var targets = ConfigJson.Lines(urls.Text);
                if (targets.Length == 0)
                    throw new FormatException(Strings.Get("Errors.WebhookRequired"));
                foreach (var url in targets)
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                        throw new FormatException(Strings.Get("Errors.WebhookUrl"));
                    await vm.Core.SendAsync(HttpMethod.Post, "webhook/test", new JsonObject { ["url"] = url });
                }
                Success(Strings.Get("Settings.WebhookSent"));
            }
            catch (Exception error) { Report(error); }
            finally { test.IsEnabled = true; }
        };
        var testRow = SettingsFields.Row(webhook, Strings.Get("Settings.TestWebhook"), test);
        SettingsFields.Reveal(webhookEnabled, webhook.Children[1], testRow);

        var script = SettingsFields.Group(page, Strings.Get("Settings.Scripts"));
        var scriptEnabled = fields.Toggle(script, Strings.Get("Settings.RunScript"), "script.enable");
        fields.Text(script, Strings.Get("Settings.ScriptPaths"), "script.paths", true);
        SettingsFields.Reveal(scriptEnabled, script.Children[1]);
    }

    private void BuildAbout()
    {
        var page = Section(Strings.Get("Settings.About"), "info");
        var product = SettingsFields.Group(page);
        var update = NativeButtons.Create(Strings.Get("Action.Check"));
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try
            {
                var release = await UpdateService.CheckAsync(vm.Core);
                if (release is null)
                    Success(Strings.Get("Update.UpToDate"));
                else
                    await UpdateService.PromptAsync(vm.Core, release, XamlRoot);
            }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        SettingsFields.Row(product, $"Harbor {typeof(App).Assembly.GetName().Version?.ToString(3)}", update, Strings.Format("Settings.CoreVersion", vm.Core.Version));
        product.Children.Add(checkUpdates);

        var links = SettingsFields.Group(page, Strings.Get("Settings.Credits"));
        var list = new StackPanel { Spacing = 4 };
        list.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.Project"), NavigateUri = new Uri("https://github.com/tavricccc/harbor"), Padding = new Thickness(0) });
        list.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.License"), NavigateUri = new Uri("https://github.com/tavricccc/harbor/blob/winui-native/LICENSE"), Padding = new Thickness(0) });
        list.Children.Add(new HyperlinkButton { Content = "Gopeed · GopeedLab", NavigateUri = new Uri("https://github.com/GopeedLab/gopeed"), Padding = new Thickness(0) });
        list.Children.Add(new HyperlinkButton { Content = "Lucide icons · ISC", NavigateUri = new Uri("https://lucide.dev/license"), Padding = new Thickness(0) });
        SettingsFields.Block(links, list);

        var maintenance = SettingsFields.Group(page, Strings.Get("Settings.Maintenance"));
        var logs = NativeButtons.Create(Strings.Get("Action.Open"));
        logs.Click += (_, _) => FileActions.Open(Path.Combine(CoreClient.DataDirectory, "logs"));
        SettingsFields.Row(maintenance, Strings.Get("Settings.OpenLogs"), logs);
        var stop = NativeButtons.Create(Strings.Get("Action.Quit"));
        stop.Click += async (_, _) =>
        {
            var dialog = new ContentDialog { Title = Strings.Get("Settings.QuitConfirm"), Content = Strings.Get("Settings.QuitDescription"), PrimaryButtonText = Strings.Get("Settings.QuitAction"), CloseButtonText = Strings.Get("Common.Cancel") };
            if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
            {
                try
                {
                    await vm.Core.StopAsync();
                    Application.Current.Exit();
                }
                catch (Exception error) { Report(error); }
            }
        };
        SettingsFields.Row(maintenance, Strings.Get("Settings.Quit"), stop, Strings.Get("Settings.QuitDescription"));
    }
}
