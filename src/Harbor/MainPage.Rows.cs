using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Models;
using Harbor.Services;

namespace Harbor;

public sealed partial class MainPage
{
    private DownloadItem? RowItem(object sender) => ViewModel.VisibleItems.FirstOrDefault(x => x.Id == (sender as Button)?.Tag?.ToString());
    private async void RowPrimary(object sender, RoutedEventArgs args)
    {
        if (RowItem(sender) is not { } item)
            return;
        try
        {
            if (item.PrimaryAction.Key == "open")
                FileActions.Open(item.OpenPath);
            else if (item.CanAct)
                await ViewModel.ActAsync(item.PrimaryAction.Key, [item]);
        }
        catch (Exception error) { ViewModel.Error = UserError.Message(error); }
    }
    private void RowFolder(object sender, RoutedEventArgs args)
    {
        try
        {
            if (RowItem(sender) is { } item)
                FileActions.Reveal(item.FilePath, item.Folder);
        }
        catch (Exception error) { ViewModel.Error = UserError.Message(error); }
    }
}
