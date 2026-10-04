using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text.Json.Nodes;
using Gopeed_Native.Models;
using Gopeed_Native.Services;
using Gopeed_Native.Views;

namespace Gopeed_Native;

public sealed partial class MainPage
{
    private void DownloadSelectionChanged(object sender, SelectionChangedEventArgs args) => ViewModel.SetSelection(DownloadList.SelectedItems.Cast<DownloadItem>());
    private void SortChanged(object sender, SelectionChangedEventArgs args) { if (((ComboBox)sender).SelectedItem is ComboBoxItem item) { ViewModel.Sort = item.Tag.ToString()!; ViewModel.ApplyFilter(); } }
    private void SelectAll(object sender, RoutedEventArgs args) => DownloadList.SelectAll();
    private bool ListHasFocus() { var current = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject; while (current is not null) { if (current == DownloadList) return true; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current); } return false; }
    private void SelectAllShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (ListHasFocus()) { DownloadList.SelectAll(); args.Handled = true; } }
    private async void DeleteShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) { if (ListHasFocus() && ViewModel.HasSelection) { args.Handled = true; await DeleteItemsAsync(ViewModel.Selection); } }
    private async Task DeleteItemsAsync(IEnumerable<DownloadItem> items)
    {
        var targets = items.ToList(); if (targets.Count == 0) return;
        var files = new CheckBox { Content = "同時刪除下載的檔案", IsChecked = !UiPreferences.Load().KeepFilesOnRemove };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = targets.Count == 1 ? targets[0].Name : string.Join("\n", targets.Take(5).Select(x => x.Name)), TextWrapping = TextWrapping.Wrap });
        if (targets.Any(x => x.CanPause)) panel.Children.Add(new TextBlock { Text = "尚未結束的下載與做種將停止。", TextWrapping = TextWrapping.Wrap });
        if (targets.Any(item => !item.IsDeferred)) panel.Children.Add(files);
        var dialog = new ContentDialog { Title = $"移除 {targets.Count} 個下載？", Content = panel, PrimaryButtonText = "移除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close };
        if (await NativeDialogs.ShowAsync(dialog, XamlRoot) != ContentDialogResult.Primary) return;
        await ViewModel.ActAsync("delete", targets, files.IsChecked == true);
        var prefs = UiPreferences.Load(); prefs.KeepFilesOnRemove = files.IsChecked != true; prefs.Save();
    }
    private async void ClearCompleted(object sender, RoutedEventArgs args)
    {
        var targets = ViewModel.AllItems.Where(x => x.IsComplete && !x.Uploading && !x.IsProcessing).ToList(); if (targets.Count == 0) return;
        var dialog = new ContentDialog { Title = $"清除 {targets.Count} 個完成紀錄？", Content = "已下載的檔案會保留。", PrimaryButtonText = "清除紀錄", CloseButtonText = "取消" };
        if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary) await ViewModel.ActAsync("delete", targets);
    }
    private async void Redownload(object sender, RoutedEventArgs args)
    {
        if (ViewModel.Selected is not { } item) return;
        var meta = item.Data["meta"]!;
        await AddDownloadAsync(new JsonObject { ["req"] = meta["req"]!.DeepClone(), ["opts"] = meta["opts"]!.DeepClone() });
    }
    private async void EditSelectedSource(object sender, RoutedEventArgs args) { if (ViewModel.Selected is { CanEditSource: true } item) await EditSource(item); }
    private async void ContextEditSource(object sender, RoutedEventArgs args) { if (ContextItem(sender) is { CanEditSource: true } item) await EditSource(item); }
    private async Task EditSource(DownloadItem item)
    {
        await NativeDialogs.ShowAsync(new DownloadSourceDialog(ViewModel.Core, item), XamlRoot);
        await ViewModel.RefreshAsync();
    }
    private async void ContextDetails(object sender, RoutedEventArgs args) { if (ContextItem(sender) is { } item) await NativeDialogs.ShowAsync(new TaskDetailsDialog(ViewModel.Core, item), XamlRoot); }
    private void ContextListenSource(object sender, RoutedEventArgs args)
    {
        if (ContextItem(sender) is not { CanEditSource: true } item) return;
        var prefs = UiPreferences.Load(); prefs.PendingUpdateTaskId = item.Id; prefs.Save();
        ErrorBar.Severity = InfoBarSeverity.Informational; ErrorBar.Message = $"等待瀏覽器下載連結：{item.Name}"; ErrorBar.IsOpen = true;
        var cancel = new Button { Content = "取消等待" }; cancel.Click += (_, _) => { var settings = UiPreferences.Load(); settings.PendingUpdateTaskId = ""; settings.Save(); ErrorBar.IsOpen = false; ErrorBar.ActionButton = null; }; ErrorBar.ActionButton = cancel;
    }
}
