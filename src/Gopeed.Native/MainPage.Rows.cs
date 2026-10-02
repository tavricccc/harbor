using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Models;
using Gopeed_Native.Services;

namespace Gopeed_Native;

public sealed partial class MainPage
{
    private void TableSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var grid = (Grid)sender;
        var showSize = args.NewSize.Width >= 860;
        grid.ColumnDefinitions[1].Width = new GridLength(showSize ? 140 : 0);
        grid.Children[1].Visibility = showSize ? Visibility.Visible : Visibility.Collapsed;
    }
    private DownloadItem? RowItem(object sender) => ViewModel.VisibleItems.FirstOrDefault(x => x.Id == (sender as Button)?.Tag?.ToString());
    private async void RowPrimary(object sender, RoutedEventArgs args)
    {
        if (RowItem(sender) is not { } item) return;
        try { if (item.PrimaryAction.Key == "open") FileActions.Open(item.OpenPath); else if (item.CanAct) await ViewModel.ActAsync(item.PrimaryAction.Key, [item]); }
        catch (Exception error) { ViewModel.Error = UserError.Message(error); }
    }
    private void RowFolder(object sender, RoutedEventArgs args) { try { if (RowItem(sender) is { } item) FileActions.Reveal(item.FilePath, item.Folder); } catch (Exception error) { ViewModel.Error = UserError.Message(error); } }
    private async void RowDelete(object sender, RoutedEventArgs args) { if (RowItem(sender) is { } item) await DeleteItemsAsync([item]); }
}
