using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Gopeed_Native.Views;

public sealed partial class SettingsPage
{
    private void BuildDownloads()
    {
        var panel = Section("下載");
        var destination = new StackPanel { Spacing = 8 };
        panel.Children.Add(destination);
        folder = fields.Text(destination, "預設下載資料夾", "downloadDir");
        var folderRow = new Grid { ColumnSpacing = 8 };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        destination.Children.Remove(folder);
        destination.Children.Add(new TextBlock { Text = "預設下載資料夾" });
        folder.Header = null; folderRow.Children.Add(folder);
        var browse = NativeButtons.Create("瀏覽…", "\uE8B7");
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
        fields.Number(connections, "每個下載的連線數（1–256）", "protocolConfig.http.connections", 8, 1, 256);
        fields.Number(concurrency, "同時下載數（1–256）", "maxRunning", 5, 1, 256);
        panel.Children.Add(remember);
        fields.Toggle(panel, "啟動時繼續上次未完成的下載", "extra.autoStartTasks");
        fields.Toggle(panel, "新增連結時直接開始下載", "extra.defaultDirectDownload");
        fields.Toggle(panel, "自動移除檔案已不存在的完成紀錄", "autoDeleteMissingFileTasks");

        var http = SettingsFields.Group(panel, "HTTP");
        fields.Text(http, "User-Agent", "protocolConfig.http.userAgent");
        fields.Toggle(http, "使用伺服器提供的檔案時間", "protocolConfig.http.useServerCtime");

        var bt = SettingsFields.Group(panel, "BitTorrent");
        fields.Number(bt, "監聽連接埠（0 為自動）", "protocolConfig.bt.listenPort", 0, 0, 65535);
        customTrackers = fields.Text(bt, "自訂 Tracker（每行一個）", "extra.bt.customTrackers", true);
        var subscriptions = fields.Text(bt, "Tracker 訂閱網址（每行一個）", "extra.bt.trackerSubscribeUrls", true);
        fields.Toggle(bt, "每天更新 Tracker 訂閱", "extra.bt.autoUpdateTrackers", true);
        var update = NativeButtons.Create("更新 Tracker 訂閱", "\uE72C");
        update.Click += async (_, _) =>
        {
            update.IsEnabled = false;
            try
            {
                var trackers = new List<string>();
                var current = (await vm.Core.GetAsync("config"))!.AsObject(); mirrors.Save(current);
                foreach (var url in ConfigJson.Lines(subscriptions.Text))
                {
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new FormatException("請輸入有效的 Tracker 訂閱網址。");
                    trackers.AddRange(ConfigJson.Lines(await vm.Core.FetchTextAsync(GitHubMirror.Apply(url, current))));
                }
                var config = (await vm.Core.GetAsync("config"))!.AsObject();
                ConfigJson.Set(config, "extra.bt.subscribeTrackers", ConfigJson.Array(trackers.Distinct()));
                ConfigJson.Set(config, "extra.bt.trackerSubscribeUrls", ConfigJson.Array(ConfigJson.Lines(subscriptions.Text)));
                ConfigJson.Set(config, "extra.bt.lastTrackerUpdateTime", JsonValue.Create(DateTimeOffset.UtcNow.ToString("O")));
                ConfigJson.Set(config, "protocolConfig.bt.trackers", ConfigJson.Array(trackers.Concat(ConfigJson.Lines(customTrackers.Text)).Distinct()));
                await vm.Core.SendAsync(HttpMethod.Put, "config", config); Success($"已更新 {trackers.Distinct().Count()} 個 Tracker。");
            }
            catch (Exception error) { Report(error); }
            finally { update.IsEnabled = true; }
        };
        bt.Children.Add(update);
        fields.Toggle(bt, "下載完成後持續做種", "protocolConfig.bt.seedKeep");
        var (ratio, seedTime) = SettingsFields.Columns(bt);
        fields.Number(ratio, "停止做種的分享比例", "protocolConfig.bt.seedRatio", 0, 0, 10000, fractional: true);
        fields.Number(seedTime, "停止做種的時間（分鐘）", "protocolConfig.bt.seedTime", 0, 0, 100000000, 60);
        var defaults = NativeButtons.Create("設定預設 Torrent 與磁力連結程式", "\uE713");
        defaults.Click += (_, _) => WindowsIntegration.OpenDefaultApps(); bt.Children.Add(defaults);

        var ed2k = SettingsFields.Group(panel, "eD2k");
        var (tcp, udp) = SettingsFields.Columns(ed2k);
        fields.Number(tcp, "TCP 連接埠（0 為自動）", "protocolConfig.ed2k.listenPort", 0, 0, 65535);
        fields.Number(udp, "UDP 連接埠（0 為自動）", "protocolConfig.ed2k.udpPort", 0, 0, 65535);
        fields.Text(ed2k, "伺服器（每行一個 host:port）", "protocolConfig.ed2k.serverAddr", true, array: false);
        fields.Text(ed2k, "server.met 網址（每行一個）", "protocolConfig.ed2k.serverMet", true, array: false);
        fields.Text(ed2k, "nodes.dat 網址（每行一個）", "protocolConfig.ed2k.nodesDat", true, array: false);
    }

    private void BuildBehavior()
    {
        var panel = Section("介面與行為");
        panel.Children.Add(theme); panel.Children.Add(startup); panel.Children.Add(closeProgress);
        fields.Toggle(panel, "下載完成時顯示通知", "extra.desktopNotification", true);
        var archive = SettingsFields.Group(panel, "壓縮檔與 Torrent");
        fields.Toggle(archive, "下載後自動解壓縮", "archive.autoExtract");
        fields.Toggle(archive, "解壓縮成功後刪除壓縮檔", "archive.deleteAfterExtract");
        fields.Toggle(archive, "下載 Torrent 檔案後自動開始 BT 下載", "autoTorrent.enable");
        fields.Toggle(archive, "開始 BT 下載後刪除 Torrent 檔案", "autoTorrent.deleteAfterDownload");
    }

    private void BuildCategories()
    {
        var panel = Section("分類");
        panel.Children.Add(SettingsFields.Description("為下載分類指定儲存位置。新增或更新分類後，按「儲存」套用。"));
        panel.Children.Add(categories);
    }
}
