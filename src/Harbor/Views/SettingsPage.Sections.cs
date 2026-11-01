using Harbor.Models;
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
        var updates = UpdateService.Shared;
        var action = NativeButtons.Create(Strings.Get("Update.Check"));
        var other = new Button { Style = (Style)Application.Current.Resources["UiTextButton"], Visibility = Visibility.Collapsed };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { other, action } };
        var row = SettingsFields.Row(product, $"Harbor {updates.CurrentVersion}", buttons, Strings.Format("Settings.CoreVersion", vm.Core.Version));
        var notes = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = 12, TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)Application.Current.Resources["SecondaryTextBlockStyle"] };
        var releaseLink = new HyperlinkButton { Content = Strings.Get("Update.ReleaseNotes"), Padding = new Thickness(0) };
        releaseLink.Click += (_, _) => { if (updates.Latest?.PageUrl is { } url) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); };
        var details = SettingsFields.Block(product, new StackPanel { Spacing = 6, Children = { notes, releaseLink } });
        product.Children.Add(checkUpdates);

        static string Name(UpdateRelease release) => release.Prerelease ? Strings.Format("Update.Preview", release.Version) : release.Version;
        void Render()
        {
            var latest = updates.Latest;
            row.Description = updates.State switch
            {
                UpdateState.Checking => Strings.Get("Update.Checking"),
                UpdateState.UpToDate => Strings.Get("Update.UpToDate"),
                UpdateState.Available when latest is not null => Strings.Format("Update.AvailableDetail", Name(latest), DownloadItem.FormatBytes(latest.SizeBytes), latest.PublishedAt.LocalDateTime.ToString("d")),
                UpdateState.Downloading when latest is not null => Strings.Format("Update.DownloadingDetail", Name(latest), $"{updates.DownloadProgress:P0}"),
                UpdateState.Ready when latest is not null => Strings.Format("Update.ReadyDetail", Name(latest)),
                UpdateState.Failed => updates.Error ?? "",
                _ => Strings.Format("Settings.CoreVersion", vm.Core.Version),
            };
            NativeButtons.SetContent(action, Strings.Get(updates.State switch
            {
                UpdateState.Checking => "Update.CheckingButton",
                UpdateState.Available => "Update.Download",
                UpdateState.Downloading => "Update.DownloadingButton",
                UpdateState.Ready => "Update.Install",
                UpdateState.Failed when latest is not null => "Update.Retry",
                _ => "Update.Check",
            }));
            var accent = updates.State is UpdateState.Available or UpdateState.Ready;
            action.Style = (Style)Application.Current.Resources[accent ? "UiAccentButton" : "UiTextButton"];
            action.IsEnabled = updates.State is not (UpdateState.Checking or UpdateState.Downloading);
            // With both a stable and a pre-release, the other one can be picked instead.
            var alternative = updates.Choices.FirstOrDefault(choice => choice != latest);
            other.Visibility = alternative is not null && updates.State is UpdateState.Available or UpdateState.Ready or UpdateState.Failed ? Visibility.Visible : Visibility.Collapsed;
            if (alternative is not null)
                NativeButtons.SetContent(other, Strings.Format("Update.SwitchTo", Name(alternative)));
            notes.Text = latest?.Notes ?? "";
            notes.Visibility = notes.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            details.Visibility = latest is not null ? Visibility.Visible : Visibility.Collapsed;
        }
        void Changed(object? sender, EventArgs args) => DispatcherQueue.TryEnqueue(Render);
        updates.Changed += Changed;
        Unloaded += (_, _) => updates.Changed -= Changed;
        Render();
        action.Click += async (_, _) =>
        {
            switch (updates.State)
            {
                case UpdateState.Available:
                case UpdateState.Failed when updates.Latest is not null:
                    await updates.DownloadAsync();
                    break;
                case UpdateState.Ready:
                    if (updates.Install())
                    {
                        try { await vm.Core.StopAsync(); }
                        catch (Exception) { /* The setup stops the core too. */ }
                        Application.Current.Exit();
                    }
                    break;
                default:
                    await updates.CheckAsync();
                    break;
            }
        };
        other.Click += (_, _) =>
        {
            if (updates.Choices.FirstOrDefault(choice => choice != updates.Latest) is { } alternative)
                updates.Choose(alternative);
        };

        var links = SettingsFields.Group(page, Strings.Get("Settings.Credits"));
        var list = new StackPanel { Spacing = 4 };
        list.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.Project"), NavigateUri = new Uri("https://github.com/tavricccc/harbor"), Padding = new Thickness(0) });
        list.Children.Add(new HyperlinkButton { Content = Strings.Get("Settings.License"), NavigateUri = new Uri("https://github.com/tavricccc/harbor/blob/harbor/LICENSE"), Padding = new Thickness(0) });
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
