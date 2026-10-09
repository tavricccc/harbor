using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Harbor.Models;
using Harbor.Services;
using System.Text.Json.Nodes;

namespace Harbor.Views;

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
        Title = Strings.Get("Details.Title");
        CloseButtonText = Strings.Get("Common.Close");
        NativeInfoBars.CollapseWhenClosed(errorBar);
        var tabs = new Pivot { MaxHeight = 440, MinWidth = 460 };
        tabs.Items.Add(new PivotItem { Header = Strings.Get("Details.Information"), Content = Scroll(Information(item)) });
        if (item.IsDeferred)
        {
            Content = new StackPanel { Spacing = 12, Children = { tabs, SettingsFields.Description(item.DeferredError.Length > 0 ? item.DeferredError : Strings.Get("Details.NotStarted")) } };
            return;
        }
        tabs.Items.Add(new PivotItem { Header = Strings.Get("Common.File"), Content = Scroll(Files(item)) });
        var connections = new StackPanel { Spacing = 12, Padding = new Thickness(0, 12, 12, 16) };
        var loading = SettingsFields.Description(Strings.Get("Details.LoadingConnections"));
        connections.Children.Add(loading);
        tabs.Items.Add(new PivotItem { Header = Strings.Get("Settings.Network"), Content = Scroll(connections) });
        Content = new StackPanel { Spacing = 16, Children = { errorBar, tabs } };
        Opened += async (_, _) =>
        {
            try
            {
                var data = await core.GetAsync<TaskStatistics>("tasks/" + item.Id + "/stats");
                connections.Children.Clear();
                if (data is null)
                {
                    connections.Children.Add(SettingsFields.Description(Strings.Get("Details.NoConnections")));
                    return;
                }
                if (item.Protocol == "BT" && data.Snapshot is { } snapshot)
                {
                    var details = new NativeFormGrid(labelWidth: 88);
                    details.AddText(Strings.Get("Details.TotalPeers"), data.Runtime?.TotalPeers.ToString() ?? "—");
                    details.AddText(Strings.Get("Details.ActivePeers"), data.Runtime?.ActivePeers.ToString() ?? "—");
                    details.AddText(Strings.Get("Details.Seeders"), data.Runtime?.ConnectedSeeders.ToString() ?? "—");
                    details.AddText(Strings.Get("Details.Shared"), DownloadItem.FormatBytes(snapshot.SeedBytes));
                    details.AddText(Strings.Get("Details.ShareRatio"), snapshot.SeedRatio.ToString());
                    details.AddText(Strings.Get("Details.SeedTime"), Strings.Format("Duration.Seconds", snapshot.SeedTime));
                    connections.Children.Add(details);
                }
                else if (data.Snapshot?.Connections is { Length: > 0 } active)
                {
                    var details = new NativeFormGrid(labelWidth: 72);
                    foreach (var (connection, index) in active.Select((value, index) => (value!, index)))
                        details.AddText(Strings.Format("Details.Connection", index + 1), Strings.Format("Details.ConnectionStats", DownloadItem.FormatBytes(connection.Downloaded), Strings.Get(connection.Completed ? "Status.Completed" : connection.Failed ? "Status.Retrying" : "Status.Downloading"), connection.RetryTimes));
                    connections.Children.Add(details);
                }
                else
                    connections.Children.Add(SettingsFields.Description(Strings.Get("Details.NoConnections")));
            }
            catch (Exception error) { connections.Children.Clear(); connections.Children.Add(SettingsFields.Description(UserError.Message(error))); }
        };
    }

    private static NativeFormGrid Information(DownloadItem item)
    {
        var form = new NativeFormGrid(labelWidth: 72) { Padding = new Thickness(0, 12, 12, 16) };
        form.AddText(Strings.Get("Common.FileName"), item.Name);
        form.AddText(Strings.Get("Common.Type"), item.Protocol);
        form.AddText(Strings.Get("Common.Status"), item.StatusText);
        form.AddText(Strings.Get("Common.Size"), item.TransferText);
        form.AddText(Strings.Get("Common.Speed"), item.SpeedText);
        form.AddText(Strings.Get("Common.Remaining"), item.RemainingText);
        form.AddText(Strings.Get("Common.Upload"), $"{DownloadItem.FormatBytes(item.Uploaded)} · {DownloadItem.FormatBytes(item.UploadSpeed)}/s");
        form.AddText(Strings.Get("Details.Created"), item.CreatedAt.LocalDateTime.ToString("g"));
        form.AddText(Strings.Get("Common.SaveTo"), item.OpenPath);
        form.AddText(Strings.Get("Common.Source"), item.Url);
        return form;
    }

    private StackPanel Files(DownloadItem item)
    {
        var entries = ReadFiles(item);
        var list = new ListView { ItemsSource = entries, MaxHeight = 244, SelectionMode = ListViewSelectionMode.Single };
        list.ItemContainerStyle = new Style { TargetType = typeof(ListViewItem), Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } };
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
        var open = NativeButtons.Create(Strings.Get("Common.Open"), "\uE8E5", true);
        var folder = NativeButtons.Create(Strings.Get("Common.RevealFile"), "\uE8B7");
        var share = NativeButtons.Create(Strings.Get("Common.Share"), "\uE72D");
        open.IsEnabled = folder.IsEnabled = share.IsEnabled = false;
        list.SelectionChanged += (_, _) =>
        {
            var entry = list.SelectedItem as FileEntry;
            selectedPath.Text = entry?.Path ?? "";
            folder.IsEnabled = entry is not null;
            open.IsEnabled = share.IsEnabled = item.IsComplete && !item.IsProcessing && entry is not null && File.Exists(entry.Path);
        };
        open.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => FileActions.Open(entry.Path)); };
        folder.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => FileActions.Reveal(entry.Path, item.Folder)); };
        share.Click += (_, _) => { if (list.SelectedItem is FileEntry entry) Run(() => ShareFiles.Show(App.WindowHandle, entry.Path)); };
        commands.Children.Add(folder);
        commands.Children.Add(share);
        commands.Children.Add(open);
        var files = new StackPanel { Spacing = 16, Padding = new Thickness(0, 12, 0, 16) };
        if (entries.Count == 0)
            files.Children.Add(SettingsFields.Description(Strings.Get("Details.FilesPending")));
        else
        {
            files.Children.Add(list);
            files.Children.Add(selectedPath);
            list.SelectedIndex = 0;
        }
        files.Children.Add(commands);
        return files;
    }

    private static List<FileEntry> ReadFiles(DownloadItem item)
    {
        var entries = new List<FileEntry>();
        var resource = item.Data["meta"]?["res"];
        var selection = item.Data["meta"]?["opts"]?["selectFiles"]?.AsArray().Select(x => x!.GetValue<int>()).ToHashSet() ?? [];
        var index = 0;
        foreach (var file in resource?["files"]?.AsArray() ?? [])
        {
            var fileIndex = index++;
            if (selection.Count > 0 && !selection.Contains(fileIndex))
                continue;
            var relative = Path.Combine(file!["path"]?.GetValue<string>() ?? "", file["name"]!.GetValue<string>());
            var path = resource?["name"]?.GetValue<string>() is { Length: > 0 } ? Path.Combine(item.FilePath, relative.TrimStart('/', '\\')) : item.FilePath;
            entries.Add(new FileEntry(relative, path, file["size"]!.GetValue<long>()));
        }
        return entries;
    }

    private static ScrollViewer Scroll(UIElement content) => new()
    {
        Content = content,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        HorizontalScrollMode = ScrollMode.Disabled
    };

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error) { errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; }
    }
}
