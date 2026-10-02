using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Gopeed_Native.Models;
using Gopeed_Native.Services;
using System.Text.Json.Nodes;

namespace Gopeed_Native.Views;

public sealed class TaskDetailsDialog : ContentDialog
{
    private readonly InfoBar errorBar = new() { Severity = InfoBarSeverity.Error, IsClosable = true };
    private sealed record FileEntry(string Name, string Path, long Size)
    {
        public string SizeText => DownloadItem.FormatBytes(Size);
        public override string ToString() => $"{Name} · {SizeText}";
    }

    public TaskDetailsDialog(CoreClient core, DownloadItem item)
    {
        Title = "下載詳情"; CloseButtonText = "關閉";
        var tabs = new Pivot { MaxHeight = 440, MinWidth = 460 };
        tabs.Items.Add(new PivotItem { Header = "資訊", Content = Scroll(Information(item)) });
        tabs.Items.Add(new PivotItem { Header = "檔案", Content = Scroll(Files(item)) });
        var connections = new StackPanel { Spacing = 12, Padding = new Thickness(0, 12, 12, 16) };
        var loading = SettingsFields.Description("正在取得連線資訊…"); connections.Children.Add(loading);
        tabs.Items.Add(new PivotItem { Header = "連線", Content = Scroll(connections) });
        Content = new StackPanel { Spacing = 16, Children = { errorBar, tabs } };
        Opened += async (_, _) =>
        {
            try
            {
                var data = await core.GetAsync("tasks/" + item.Id + "/stats");
                connections.Children.Clear();
                if (data is null) { connections.Children.Add(SettingsFields.Description("目前沒有連線資訊。")); return; }
                if (item.Protocol == "BT")
                {
                    var details = new NativeFormGrid(labelWidth: 88);
                    details.AddText("已知節點", data["totalPeers"]!.ToString());
                    details.AddText("連線節點", data["activePeers"]!.ToString());
                    details.AddText("完整種子", data["connectedSeeders"]!.ToString());
                    details.AddText("已分享", DownloadItem.FormatBytes(data["seedBytes"]!.GetValue<long>()));
                    details.AddText("分享比例", data["seedRatio"]!.ToString());
                    details.AddText("做種時間", $"{data["seedTime"]} 秒");
                    connections.Children.Add(details);
                }
                else if (data["connections"] is JsonArray active)
                {
                    if (active.Count == 0) connections.Children.Add(SettingsFields.Description("目前沒有連線資訊。"));
                    var details = new NativeFormGrid(labelWidth: 72);
                    foreach (var (connection, index) in active.Select((value, index) => (value!, index)))
                        details.AddText($"連線 {index + 1}", $"{DownloadItem.FormatBytes(connection["downloaded"]!.GetValue<long>())} · {(connection["completed"]!.GetValue<bool>() ? "已完成" : connection["failed"]!.GetValue<bool>() ? "重試中" : "下載中")} · 重試 {connection["retryTimes"]} 次");
                    connections.Children.Add(details);
                }
                else connections.Children.Add(SettingsFields.Description("目前沒有連線資訊。"));
            }
            catch (Exception error) { connections.Children.Clear(); connections.Children.Add(SettingsFields.Description(UserError.Message(error))); }
        };
    }

    private static NativeFormGrid Information(DownloadItem item)
    {
        var form = new NativeFormGrid(labelWidth: 72) { Padding = new Thickness(0, 12, 12, 16) };
        form.AddText("檔名", item.Name);
        form.AddText("類型", item.Protocol);
        form.AddText("狀態", item.StatusText);
        form.AddText("大小", item.TransferText);
        form.AddText("速度", item.SpeedText);
        form.AddText("剩餘", item.RemainingText);
        form.AddText("上傳", $"{DownloadItem.FormatBytes(item.Uploaded)} · {DownloadItem.FormatBytes(item.UploadSpeed)}/s");
        form.AddText("新增時間", item.CreatedAt.LocalDateTime.ToString("g"));
        form.AddText("存到", item.OpenPath);
        form.AddText("來源", item.Url);
        return form;
    }

    private StackPanel Files(DownloadItem item)
    {
        var entries = ReadFiles(item);
        var list = new ListView { ItemsSource = entries, MaxHeight = 244, SelectionMode = ListViewSelectionMode.Single };
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="4,8" ColumnSpacing="16">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Name}" FontSize="14" TextTrimming="CharacterEllipsis"/>
                    <TextBlock Grid.Column="1" Text="{Binding SizeText}" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" VerticalAlignment="Center"/>
                </Grid>
            </DataTemplate>
            """);
        var selectedPath = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Style = (Style)Application.Current.Resources["SecondaryTextBlockStyle"] };
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var open = NativeButtons.Create("開啟", "\uE8E5", true);
        var folder = NativeButtons.Create("在資料夾中顯示", "\uE8B7");
        var share = NativeButtons.Create("分享", "\uE72D");
        open.IsEnabled = folder.IsEnabled = share.IsEnabled = false;
        list.SelectionChanged += (_, _) =>
        {
            var entry = list.SelectedItem as FileEntry; selectedPath.Text = entry?.Path ?? "";
            folder.IsEnabled = entry is not null;
            open.IsEnabled = share.IsEnabled = item.IsComplete && !item.IsProcessing && entry is not null && File.Exists(entry.Path);
        };
        open.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => FileActions.Open(entry.Path)); };
        folder.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => FileActions.Reveal(entry.Path, item.Folder)); };
        share.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => ShareFiles.Show(App.WindowHandle, entry.Path)); };
        commands.Children.Add(folder); commands.Children.Add(share); commands.Children.Add(open);
        var files = new StackPanel { Spacing = 16, Padding = new Thickness(0, 12, 0, 16) };
        if (entries.Count == 0) files.Children.Add(SettingsFields.Description("取得檔案資訊後將顯示在這裡。"));
        else { files.Children.Add(list); files.Children.Add(selectedPath); list.SelectedIndex = 0; }
        files.Children.Add(commands); return files;
    }

    private static List<FileEntry> ReadFiles(DownloadItem item)
    {
        var entries = new List<FileEntry>(); var resource = item.Data["meta"]?["res"];
        var selection = item.Data["meta"]?["opts"]?["selectFiles"]?.AsArray().Select(x => x!.GetValue<int>()).ToHashSet() ?? [];
        var index = 0;
        foreach (var file in resource?["files"]?.AsArray() ?? [])
        {
            var fileIndex = index++; if (selection.Count > 0 && !selection.Contains(fileIndex)) continue;
            var relative = Path.Combine(file!["path"]?.GetValue<string>() ?? "", file["name"]!.GetValue<string>());
            var path = resource?["name"]?.GetValue<string>() is { Length: > 0 } ? Path.Combine(item.FilePath, relative.TrimStart('/', '\\')) : item.FilePath;
            entries.Add(new FileEntry(relative, path, file["size"]!.GetValue<long>()));
        }
        return entries;
    }

    private static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled
    };

    private void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; }
    }
}
