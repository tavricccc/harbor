using Harbor.Localization;
using Harbor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Harbor.Views;

internal sealed class DownloadConnectionsPanel : Grid
{
    private readonly ObservableCollection<ConnectionProgress> rows = [];
    private readonly TextBlock message = SettingsFields.Description(Strings.Get("Details.LoadingConnections"));
    private readonly ListView list;
    public event Action? LayoutChanged;

    public DownloadConnectionsPanel()
    {
        MaxHeight = 240;
        list = new ListView { ItemsSource = rows, SelectionMode = ListViewSelectionMode.None, Visibility = Visibility.Collapsed };
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollMode(list, ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        list.ItemContainerStyle = new Style
        {
            TargetType = typeof(ListViewItem),
            Setters =
        {
            new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
            new Setter(Control.PaddingProperty, new Thickness(0)),
            new Setter(FrameworkElement.MinHeightProperty, 0d)
        }
        };
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="0,4" ColumnSpacing="12" ToolTipService.ToolTip="{Binding TransferText}">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="96"/><ColumnDefinition Width="*"/><ColumnDefinition Width="80"/></Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Label}" FontSize="12" Foreground="{ThemeResource UiText2Brush}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                    <ProgressBar Grid.Column="1" Value="{Binding Percent}" Maximum="100" IsIndeterminate="{Binding IsIndeterminate}" VerticalAlignment="Center" AutomationProperties.Name="{Binding Label}"/>
                    <TextBlock Grid.Column="2" Text="{Binding SpeedText}" FontSize="12" Foreground="{ThemeResource UiText2Brush}" HorizontalAlignment="Right" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                </Grid>
            </DataTemplate>
            """);
        Children.Add(message);
        Children.Add(list);
    }

    public void Update(TaskStatistics? stats, DownloadItem item)
    {
        var http = item.Protocol == "HTTP";
        var connections = stats?.Snapshot?.Connections ?? [];
        var peers = stats?.Runtime?.Peers ?? [];
        var count = http ? connections.Length : peers.Length;
        var changed = rows.Count != count;
        while (rows.Count > count)
            rows.RemoveAt(rows.Count - 1);
        while (rows.Count < count)
            rows.Add(new ConnectionProgress());
        var now = Stopwatch.GetTimestamp();
        for (var index = 0; index < count; index++)
        {
            if (http)
                rows[index].UpdateHttp(connections[index], index, now, item.Status == "running", item.Size);
            else
                rows[index].UpdatePeer(peers[index]);
        }
        message.Text = Strings.Get("Details.NoConnections");
        message.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        list.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (changed)
            LayoutChanged?.Invoke();
    }

    public void ResetSamples()
    {
        foreach (var row in rows)
            row.ResetSample();
    }
}
