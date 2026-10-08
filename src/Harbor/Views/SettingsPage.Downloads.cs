using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed partial class SettingsPage
{
    private void BuildDownloads()
    {
        var panel = Section(Strings.Get("Common.Download"));
        var destination = new StackPanel { Spacing = 8 };
        panel.Children.Add(destination);
        folder = fields.Text(destination, Strings.Get("Settings.DownloadFolder"), "downloadDir");
        var folderRow = new Grid { ColumnSpacing = 8 };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        destination.Children.Remove(folder);
        destination.Children.Add(new TextBlock { Text = Strings.Get("Settings.DownloadFolder") });
        folder.Header = null; folderRow.Children.Add(folder);
        var browse = NativeButtons.Create(Strings.Get("Common.Browse"), "\uE8B7");
        browse.Click += async (_, _) =>
        {
            try
            {
                var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
                var picked = await picker.PickSingleFolderAsync(); if (picked is not null) folder.Text = picked.Path;
            }
            catch (Exception error) { Report(error); }
        };
        Grid.SetColumn(browse, 1); folderRow.Children.Add(browse); destination.Children.Add(folderRow);
        var (connections, concurrency) = SettingsFields.Columns(panel);
        fields.Number(connections, Strings.Get("Settings.Connections"), "protocolConfig.http.connections", 8, 1, 256);
        fields.Number(concurrency, Strings.Get("Settings.Concurrent"), "maxRunning", 5, 1, 256);
        panel.Children.Add(remember);
        fields.Toggle(panel, Strings.Get("Settings.AutoResume"), "extra.autoStartTasks");
        fields.Toggle(panel, Strings.Get("Settings.SkipCheck"), "extra.defaultDirectDownload");
        fields.Toggle(panel, Strings.Get("Settings.RemoveMissing"), "autoDeleteMissingFileTasks");

        var http = SettingsFields.Advanced(panel, Strings.Get("Settings.Http"));
        fields.Text(http, "User-Agent", "protocolConfig.http.userAgent");
        fields.Toggle(http, Strings.Get("Settings.ServerTime"), "protocolConfig.http.useServerCtime");

        var bt = SettingsFields.Advanced(panel, Strings.Get("Settings.BitTorrent"));
        fields.Number(bt, Strings.Get("Settings.ListenPort"), "protocolConfig.bt.listenPort", 0, 0, 65535);
        customTrackers = fields.Text(bt, Strings.Get("Settings.CustomTrackers"), "extra.bt.customTrackers", true);
        var subscriptions = fields.Text(bt, Strings.Get("Settings.TrackerUrls"), "extra.bt.trackerSubscribeUrls", true);
        fields.Toggle(bt, Strings.Get("Settings.UpdateTrackersDaily"), "extra.bt.autoUpdateTrackers", true);
        var update = NativeButtons.Create(Strings.Get("Settings.UpdateTrackers"), "\uE72C");
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try
            {
                var trackers = new List<string>();
                var current = (await vm.Core.GetAsync("config"))!.AsObject(); mirrors.Save(current);
                foreach (var url in ConfigJson.Lines(subscriptions.Text))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new FormatException(Strings.Get("Errors.TrackerUrl"));
                    trackers.AddRange(ConfigJson.Lines(await vm.Core.FetchTextAsync(GitHubMirror.Apply(url, current))));
                }
                var config = (await vm.Core.GetAsync("config"))!.AsObject();
                ConfigJson.Set(config, "extra.bt.subscribeTrackers", ConfigJson.Array(trackers.Distinct()));
                ConfigJson.Set(config, "extra.bt.trackerSubscribeUrls", ConfigJson.Array(ConfigJson.Lines(subscriptions.Text)));
                ConfigJson.Set(config, "extra.bt.lastTrackerUpdateTime", JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")));
                ConfigJson.Set(config, "protocolConfig.bt.trackers", ConfigJson.Array(trackers.Concat(ConfigJson.Lines(customTrackers.Text)).Distinct()));
                await vm.Core.SendAsync(HttpMethod.Put, "config", config); Success(Strings.Format("Settings.TrackersUpdated", trackers.Distinct().Count()));
            }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        bt.Children.Add(update);
        fields.Toggle(bt, Strings.Get("Settings.KeepSeeding"), "protocolConfig.bt.seedKeep");
        var (ratio, seedTime) = SettingsFields.Columns(bt);
        fields.Number(ratio, Strings.Get("Settings.SeedRatio"), "protocolConfig.bt.seedRatio", 0, 0, 10000, fractional: true);
        fields.Number(seedTime, Strings.Get("Settings.SeedTime"), "protocolConfig.bt.seedTime", 0, 0, 100000000, 60);
        var defaults = NativeButtons.Create(Strings.Get("Settings.DefaultTorrentApp"), "\uE713");
        defaults.Click += (_, _) => WindowsIntegration.OpenDefaultApps(); bt.Children.Add(defaults);

        var ed2k = SettingsFields.Advanced(panel, Strings.Get("Settings.Ed2k"));
        var (tcp, udp) = SettingsFields.Columns(ed2k);
        fields.Number(tcp, Strings.Get("Settings.TcpPort"), "protocolConfig.ed2k.listenPort", 0, 0, 65535);
        fields.Number(udp, Strings.Get("Settings.UdpPort"), "protocolConfig.ed2k.udpPort", 0, 0, 65535);
        fields.Text(ed2k, Strings.Get("Settings.Servers"), "protocolConfig.ed2k.serverAddr", true, array: false);
        fields.Text(ed2k, Strings.Get("Settings.ServerMet"), "protocolConfig.ed2k.serverMet", true, array: false);
        fields.Text(ed2k, Strings.Get("Settings.NodesDat"), "protocolConfig.ed2k.nodesDat", true, array: false);
    }

    private void BuildBehavior()
    {
        var panel = Section(Strings.Get("Settings.Interface"));
        panel.Children.Add(language);
        panel.Children.Add(SettingsFields.Description(Strings.Get("Settings.LanguageHint")));
        panel.Children.Add(theme); panel.Children.Add(startup); panel.Children.Add(closeProgress);
        fields.Toggle(panel, Strings.Get("Settings.Notifications"), "extra.desktopNotification", true);
        var archive = SettingsFields.Group(panel, Strings.Get("Settings.ArchivesTorrent"));
        fields.Toggle(archive, Strings.Get("Settings.ExtractAfter"), "archive.autoExtract");
        fields.Toggle(archive, Strings.Get("Archive.DeleteAfter"), "archive.deleteAfterExtract");
        fields.Toggle(archive, Strings.Get("Settings.StartTorrent"), "autoTorrent.enable");
        fields.Toggle(archive, Strings.Get("Torrent.DeleteFile"), "autoTorrent.deleteAfterDownload");
    }

    private void BuildCategories()
    {
        var panel = Section(Strings.Get("Common.Category"));
        panel.Children.Add(SettingsFields.Description(Strings.Get("Categories.Description")));
        panel.Children.Add(categories);
    }
}
