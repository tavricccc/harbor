using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class SettingsPage
{
    private void BuildDownloads()
    {
        var page = Section(Strings.Get("Common.Download"), "download");
        var general = SettingsFields.Group(page);
        folder = new TextBox();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(folder, "Setting-downloadDir");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(folder, Strings.Get("Settings.DownloadFolder"));
        var folderRow = new Grid { ColumnSpacing = 8 };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        folderRow.Children.Add(folder);
        var browse = NativeButtons.Create(Strings.Get("Common.Browse"));
        browse.Click += async (_, _) =>
        {
            browse.IsEnabled = false;
            try
            {
                if (await NativePickers.FolderAsync(windowHandle, folder.Text) is { } path)
                    folder.Text = path;
            }
            catch (Exception error) { Report(error); }
            finally { browse.IsEnabled = true; }
        };
        Grid.SetColumn(browse, 1);
        folderRow.Children.Add(browse);
        SettingsFields.Row(general, Strings.Get("Settings.DownloadFolder"), folderRow, stacked: true);
        fields.Bind(folder, "downloadDir");
        general.Children.Add(remember);
        fields.Number(general, Strings.Get("Settings.Connections"), "protocolConfig.http.connections", 8, 1, 256);
        fields.Number(general, Strings.Get("Settings.Concurrent"), "maxRunning", 5, 1, 256);
        fields.Toggle(general, Strings.Get("Settings.AutoResume"), "extra.autoStartTasks");
        fields.Toggle(general, Strings.Get("Settings.SkipCheck"), "extra.defaultDirectDownload");
        fields.Toggle(general, Strings.Get("Settings.RemoveMissing"), "autoDeleteMissingFileTasks");

        var http = SettingsFields.Group(page, Strings.Get("Settings.Http"));
        fields.Text(http, "User-Agent", "protocolConfig.http.userAgent");
        fields.Toggle(http, Strings.Get("Settings.ServerTime"), "protocolConfig.http.useServerCtime");

        var bt = SettingsFields.Group(page, Strings.Get("Settings.BitTorrent"));
        fields.Number(bt, Strings.Get("Settings.ListenPort"), "protocolConfig.bt.listenPort", 0, 0, 65535);
        fields.Toggle(bt, Strings.Get("Settings.KeepSeeding"), "protocolConfig.bt.seedKeep");
        fields.Number(bt, Strings.Get("Settings.SeedRatio"), "protocolConfig.bt.seedRatio", 0, 0, 10000, fractional: true);
        fields.Number(bt, Strings.Get("Settings.SeedTime"), "protocolConfig.bt.seedTime", 0, 0, 100000000, 60);
        var defaults = NativeButtons.Create(Strings.Get("Action.Change"));
        defaults.Click += (_, _) => WindowsIntegration.OpenDefaultApps();
        SettingsFields.Row(bt, Strings.Get("Settings.DefaultTorrentApp"), defaults);

        var trackers = SettingsFields.Group(page, Strings.Get("Settings.Trackers"));
        customTrackers = fields.Text(trackers, Strings.Get("Settings.CustomTrackers"), "extra.bt.customTrackers", true);
        var subscriptions = fields.Text(trackers, Strings.Get("Settings.TrackerUrls"), "extra.bt.trackerSubscribeUrls", true);
        fields.Toggle(trackers, Strings.Get("Settings.UpdateTrackersDaily"), "extra.bt.autoUpdateTrackers", true);
        var update = NativeButtons.Create(Strings.Get("Action.UpdateNow"));
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try
            {
                var list = new List<string>();
                var current = (await vm.Core.GetAsync("config"))!.AsObject();
                mirrors.Save(current);
                foreach (var url in ConfigJson.Lines(subscriptions.Text))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                        throw new FormatException(Strings.Get("Errors.TrackerUrl"));
                    list.AddRange(ConfigJson.Lines(await vm.Core.FetchTextAsync(GitHubMirror.Apply(url, current))));
                }
                var config = (await vm.Core.GetAsync("config"))!.AsObject();
                ConfigJson.Set(config, "extra.bt.subscribeTrackers", ConfigJson.Array(list.Distinct()));
                ConfigJson.Set(config, "extra.bt.trackerSubscribeUrls", ConfigJson.Array(ConfigJson.Lines(subscriptions.Text)));
                ConfigJson.Set(config, "extra.bt.lastTrackerUpdateTime", JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")));
                ConfigJson.Set(config, "protocolConfig.bt.trackers", ConfigJson.Array(list.Concat(ConfigJson.Lines(customTrackers.Text)).Distinct()));
                await vm.Core.SendAsync(HttpMethod.Put, "config", config);
                Success(Strings.Format("Settings.TrackersUpdated", list.Distinct().Count()));
            }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        SettingsFields.Row(trackers, Strings.Get("Settings.UpdateTrackers"), update);

        var ed2k = SettingsFields.Group(page, Strings.Get("Settings.Ed2k"));
        fields.Number(ed2k, Strings.Get("Settings.TcpPort"), "protocolConfig.ed2k.listenPort", 0, 0, 65535);
        fields.Number(ed2k, Strings.Get("Settings.UdpPort"), "protocolConfig.ed2k.udpPort", 0, 0, 65535);
        fields.Text(ed2k, Strings.Get("Settings.Servers"), "protocolConfig.ed2k.serverAddr", true, array: false);
        fields.Text(ed2k, Strings.Get("Settings.ServerMet"), "protocolConfig.ed2k.serverMet", true, array: false);
        fields.Text(ed2k, Strings.Get("Settings.NodesDat"), "protocolConfig.ed2k.nodesDat", true, array: false);
    }

    private void BuildBehavior()
    {
        var page = Section(Strings.Get("Settings.Interface"), "palette");
        var appearance = SettingsFields.Group(page, note: Strings.Get("Settings.LanguageHint"));
        SettingsFields.Row(appearance, Strings.Get("Settings.Language"), language);
        SettingsFields.Row(appearance, Strings.Get("Settings.Theme"), theme);
        var behavior = SettingsFields.Group(page, Strings.Get("Settings.Behavior"));
        behavior.Children.Add(startup);
        behavior.Children.Add(closeProgress);
        fields.Toggle(behavior, Strings.Get("Settings.Notifications"), "extra.desktopNotification", true);
        var archive = SettingsFields.Group(page, Strings.Get("Settings.ArchivesTorrent"));
        fields.Toggle(archive, Strings.Get("Settings.ExtractAfter"), "archive.autoExtract");
        fields.Toggle(archive, Strings.Get("Archive.DeleteAfter"), "archive.deleteAfterExtract");
        fields.Toggle(archive, Strings.Get("Settings.StartTorrent"), "autoTorrent.enable");
        fields.Toggle(archive, Strings.Get("Torrent.DeleteFile"), "autoTorrent.deleteAfterDownload");
    }

    private void BuildCategories()
    {
        var page = Section(Strings.Get("Common.Category"), "folder");
        var card = SettingsFields.Group(page);
        card.Children.Add(categories.Enabled);
        SettingsFields.Reveal(categories.Enabled, SettingsFields.Block(card, categories));
    }
}
