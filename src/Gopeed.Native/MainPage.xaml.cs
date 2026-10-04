using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gopeed_Native.Models;
using Gopeed_Native.ViewModels;
using Gopeed_Native.Views;
using Gopeed_Native.Services;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace Gopeed_Native;

public sealed partial class MainPage : Page
{
 public DownloadsViewModel ViewModel { get; } = new();
 private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
 private bool refreshing;
 private SettingsWindow? settingsWindow;
 private readonly TaskCompletionSource ready = new();
 public MainPage()
 {
  InitializeComponent(); Loaded += Start;
  NativeInfoBars.CollapseWhenClosed(ErrorBar);
  InitializeBrowserSetupHint();
  Unloaded += (_, _) => { timer.Stop(); settingsWindow?.Close(); ViewModel.Dispose(); };
  ViewModel.VisibleItems.CollectionChanged += (_, _) => EmptyState.Visibility = ViewModel.VisibleItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
  ViewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ViewModel.Error) && ViewModel.Error.Length > 0) { ErrorBar.Message = ViewModel.Error; ErrorBar.IsOpen = true; } };
  timer.Tick += async (_, _) => { if (refreshing || !ViewModel.IsConnected) return; refreshing = true; await ViewModel.RefreshAsync(); refreshing = false; };
 }
 private async void Start(object sender, RoutedEventArgs e) { Loaded -= Start; await ViewModel.InitializeAsync(); timer.Start(); ready.SetResult(); if (ViewModel.IsConnected && UiPreferences.Load().CheckForUpdates) { try { var update = await UpdateService.CheckAsync(ViewModel.Core); if (update is not null) { ErrorBar.Severity = InfoBarSeverity.Informational; ErrorBar.Message = $"有新版本：{update.Version}"; var button = new Button { Content = "下載更新" }; button.Click += async (_, _) => await UpdateService.PromptAsync(ViewModel.Core, update, XamlRoot); ErrorBar.ActionButton = button; ErrorBar.IsOpen = true; } } catch (Exception) { /* A background update check must not interrupt downloads. Manual checks report errors. */ } } }
 private async void AddDownload(object sender, RoutedEventArgs e)
 {
  await AddDownloadAsync(null);
 }
 private async Task AddDownloadAsync(System.Text.Json.Nodes.JsonObject? parameters)
 {
  await ready.Task;
  if (!ViewModel.IsConnected) return;
  ((App)Application.Current).OpenDownloadWindow(parameters ?? new System.Text.Json.Nodes.JsonObject(), compact: false);
 }
 public async void OpenProtocol(string value)
 {
  try
  {
   var link = GopeedLink.Parse(value); await ready.Task;
   if (link.Route == "extension")
   {
    OpenSection(new ExtensionsPage(ViewModel, link.Parameters?["url"]?.GetValue<string>()));
   }
   else
   {
    ShowDownloads(this, new());
    if (link.Route == "create") ((App)Application.Current).OpenDownloadWindow(link.Parameters ?? new System.Text.Json.Nodes.JsonObject());
   }
  }
  catch (Exception error) { ViewModel.Error = $"無法開啟 Gopeed 連結：{error.Message}"; }
 }
 private void FilterChanged(object s, SelectionChangedEventArgs e) { if (FilterBox?.SelectedItem is ComboBoxItem item) { ViewModel.Filter = item.Tag.ToString()!; ViewModel.ApplyFilter(); } }
 private void SearchChanged(AutoSuggestBox s, AutoSuggestBoxTextChangedEventArgs e) { ViewModel.Search = s.Text; ViewModel.ApplyFilter(); }
 private async void PauseSelected(object s, RoutedEventArgs e) => await ViewModel.ActAsync("pause", ViewModel.Selection.Where(x => x.CanPause));
 private async void ResumeSelected(object s, RoutedEventArgs e) => await ViewModel.ActAsync("continue", ViewModel.Selection.Where(x => x.CanResume));
 private async void PauseAll(object s, RoutedEventArgs e) => await ViewModel.ActAsync("pause", ViewModel.AllItems.Where(i => i.CanPause));
 private async void ResumeAll(object s, RoutedEventArgs e) => await ViewModel.ActAsync("continue", ViewModel.AllItems.Where(i => i.CanResume && (!i.IsDeferred || i.ScheduledAt is null)));
 private async void RefreshClicked(object s, RoutedEventArgs e) => await ViewModel.RefreshAsync();
 private void OpenSelected(object s, RoutedEventArgs e) { try { if (ViewModel.Selected is { IsComplete: true } item) FileActions.Open(item.OpenPath); } catch (Exception ex) { ViewModel.Error = UserError.Message(ex); } }
 private void CopySelected(object s, RoutedEventArgs e) => FileActions.Copy(string.Join("\n", ViewModel.Selection.Select(x => x.Url)));
 private async void PasteDownload(object s, RoutedEventArgs e)
 {
  try {
   var content = Clipboard.GetContent();
   if (content.Contains(StandardDataFormats.StorageItems)) {
    var files = await content.GetStorageItemsAsync();
    if (files.Count == 0 || files.Any(file => !file.Path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))) throw new FormatException("請複製 Torrent 檔案或下載連結。");
    await AddText(string.Join("\n", files.Select(file => file.Path)));
   }
   else if (content.Contains(StandardDataFormats.WebLink)) await AddText((await content.GetWebLinkAsync()).AbsoluteUri);
   else if (content.Contains(StandardDataFormats.Text)) await AddText(await content.GetTextAsync());
   else throw new FormatException("剪貼簿沒有下載連結或 Torrent 檔案。");
  }
  catch (Exception ex) { ViewModel.Error = UserError.Message(ex); }
 }
 private async Task AddText(string text)
 {
  var links = text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(link => link.Trim('"')).ToArray();
  if (links.Length == 0 || links.Any(link => !DownloadSources.IsSupported(link))) throw new FormatException("請貼上 HTTP、HTTPS、磁力、eD2k 下載連結或 Torrent 檔案路徑。");
  await AddDownloadAsync(new System.Text.Json.Nodes.JsonObject { ["req"] = new System.Text.Json.Nodes.JsonObject { ["url"] = string.Join("\n", links) } });
 }
 private void DownloadDragOver(object s, DragEventArgs e) { e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.Text) || e.DataView.Contains(StandardDataFormats.WebLink) || e.DataView.Contains(StandardDataFormats.StorageItems) ? DataPackageOperation.Copy : DataPackageOperation.None; }
 private async void DownloadDrop(object s, DragEventArgs e)
 {
  var deferral = e.GetDeferral();
  string? text = null;
  System.Text.Json.Nodes.JsonObject? parameters = null;
  try
  {
   if (e.DataView.Contains(StandardDataFormats.WebLink)) text = (await e.DataView.GetWebLinkAsync()).AbsoluteUri;
   else if (e.DataView.Contains(StandardDataFormats.Text)) text = await e.DataView.GetTextAsync();
   else if (e.DataView.Contains(StandardDataFormats.StorageItems))
   {
    var files = await e.DataView.GetStorageItemsAsync();
    if (files.Count == 0 || files.Any(f => !f.Path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))) throw new FormatException("拖放檔案目前接受 torrent 檔案。");
    parameters = new System.Text.Json.Nodes.JsonObject { ["req"] = new System.Text.Json.Nodes.JsonObject { ["url"] = string.Join("\n", files.Select(f => f.Path)) } };
   }
  }
  catch (Exception ex) { ViewModel.Error = UserError.Message(ex); return; }
  finally { deferral.Complete(); }
  try { if (parameters is not null) await AddDownloadAsync(parameters); else if (text is not null) await AddText(text); }
  catch (Exception ex) { ViewModel.Error = UserError.Message(ex); }
 }
 private async void DeleteSelected(object s, RoutedEventArgs e) => await DeleteItemsAsync(ViewModel.Selection);
 private async Task DeleteAsync(DownloadItem item)
 {
  await DeleteItemsAsync([item]);
 }
 private async void ShowDetails(object s, RoutedEventArgs e)
 {
  if (ViewModel.Selected is not { } item) return;
  await NativeDialogs.ShowAsync(new TaskDetailsDialog(ViewModel.Core, item), XamlRoot);
 }
 private void ListDoubleTapped(object s, DoubleTappedRoutedEventArgs e) { if (ViewModel.Selected?.IsComplete == true) OpenSelected(s, new()); else ShowDetails(s, new()); }
 private DownloadItem? ContextItem(object s) => ViewModel.VisibleItems.FirstOrDefault(i => i.Id == (s as MenuFlyoutItem)?.Tag?.ToString());
 private async void ContextPrimary(object s, RoutedEventArgs e)
 {
  if (ContextItem(s) is not { } item) return;
  var action = item.PrimaryAction;
  if (action.Key == "open") { try { FileActions.Open(item.OpenPath); } catch (Exception ex) { ViewModel.Error = UserError.Message(ex); } }
  else if (action.Key != "none") await ViewModel.ActAsync(action.Key, [item]);
 }
 private async void ContextPause(object s, RoutedEventArgs e) { if (ContextItem(s) is { CanPause: true } item) await ViewModel.ActAsync("pause", [item]); }
 private async void ContextResume(object s, RoutedEventArgs e) { if (ContextItem(s) is { CanResume: true } item) await ViewModel.ActAsync("continue", [item]); }
 private void ContextFolder(object s, RoutedEventArgs e) { try { if (ContextItem(s) is { } item) FileActions.Reveal(item.FilePath, item.Folder); } catch (Exception ex) { ViewModel.Error = UserError.Message(ex); } }
 private async void ContextDelete(object s, RoutedEventArgs e) { if (ContextItem(s) is { } item) await DeleteAsync(item); }
 private void NewShortcut(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { AddDownload(s, new()); e.Handled = true; }
 private void SearchShortcut(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { SearchBox.Focus(FocusState.Keyboard); e.Handled = true; }
 private async void RefreshShortcut(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { await ViewModel.RefreshAsync(); e.Handled = true; }
 private void OpenSection(Page page) { DownloadsSurface.Visibility = Visibility.Collapsed; DownloadsCommands.Visibility = Visibility.Collapsed; SettingsFrame.Content = page; SettingsFrame.Visibility = Visibility.Visible; BackToDownloads.Visibility = Visibility.Visible; }
 private void ShowDownloads(object sender, RoutedEventArgs args) { SettingsFrame.Content = null; SettingsFrame.Visibility = Visibility.Collapsed; DownloadsSurface.Visibility = Visibility.Visible; DownloadsCommands.Visibility = Visibility.Visible; BackToDownloads.Visibility = Visibility.Collapsed; }
 private void ShowSettings(object sender, RoutedEventArgs args)
 {
  if (settingsWindow is not null) { settingsWindow.Activate(); return; }
  settingsWindow = new SettingsWindow(ViewModel);
  settingsWindow.Closed += (_, _) => settingsWindow = null;
  settingsWindow.Activate();
 }
 private void ShowExtensions(object sender, RoutedEventArgs args) => OpenSection(new ExtensionsPage(ViewModel));
}
