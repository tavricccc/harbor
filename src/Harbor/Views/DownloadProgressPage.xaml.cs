using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Models;
using Harbor.Services;
using System.Net.Http;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Harbor.Views;

public sealed partial class DownloadProgressPage : Page
{
    private readonly CoreClient core;
    private readonly string id;
    private readonly Action close;
    private readonly Func<bool> windowVisible;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DownloadItem? item;
    private bool refreshing;
    private bool cancelling;
    private bool stopped;
    private bool loaded;
    public event Action? LayoutChanged;
    public event Action<string>? TitleChanged;

    public DownloadProgressPage(CoreClient core, string id, Action close, Func<bool> windowVisible)
    {
        InitializeComponent();
        this.core = core; this.id = id; this.close = close; this.windowVisible = windowVisible;
        CloseAfterOpen.IsChecked = UiPreferences.Load().CloseProgressAfterOpen;
        Body.SizeChanged += (_, _) => LayoutChanged?.Invoke();
        timer.Tick += async (_, _) => { if (windowVisible()) await Refresh(); else timer.Stop(); };
        Loaded += (_, _) => { loaded = true; UpdatePollingVisibility(); };
        Unloaded += (_, _) => Stop();
    }

    public double PreferredHeight(double width)
    {
        Body.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        Footer.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        return Body.DesiredSize.Height + Footer.DesiredSize.Height;
    }

    private async Task Refresh()
    {
        if (refreshing || stopped || cancelling) return; refreshing = true;
        try
        {
            var snapshot = (await core.GetAsync("tasks/" + id))!.AsObject();
            if (stopped || cancelling) return;
            var changes = item is null ? DownloadChanges.Content | DownloadChanges.Progress : item.Update(snapshot);
            item ??= new DownloadItem(snapshot);
            if (changes == DownloadChanges.None) return;
            Primary.Content = item.PrimaryAction.Label;
            Primary.IsEnabled = item.CanAct && !cancelling;
            FileName.Text = item.Name; FileName.CanDrag = item.IsComplete && !item.IsProcessing;
            KindLabel.Text = item.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || item.Url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ? "BT" : "檔案";
            ToolTipService.SetToolTip(FileName, item.Name);
            Folder.Text = $"存到：{item.Folder}"; ToolTipService.SetToolTip(Folder, item.Folder); Source.Text = item.Url;
            Transfer.Text = item.TransferSizeText;
            Speed.Text = $"{(item.Uploading ? "上傳" : "下載")}：{item.SpeedText}";
            Remaining.Text = item.RemainingText == "—" ? item.Status == "running" ? "計算中" : item.StatusText : item.RemainingText;
            var finished = item.IsComplete && !item.IsProcessing && !item.Uploading;
            Cancel.Content = finished ? "關閉" : "取消";
            Cancel.IsEnabled = !cancelling;
            ToolTipService.SetToolTip(Cancel, finished ? "關閉此視窗" : "停止並移除下載任務，保留已下載的檔案");
            Primary.Style = (Style)Application.Current.Resources[finished || item.Status == "error" ? "AccentButtonStyle" : "DefaultButtonStyle"];
            Speed.Visibility = finished ? Visibility.Collapsed : Visibility.Visible;
            Remaining.Visibility = item.IsComplete ? Visibility.Collapsed : Visibility.Visible;
            Metrics.ColumnDefinitions[1].Width = finished ? new GridLength(0) : GridLength.Auto;
            Metrics.ColumnDefinitions[2].Width = item.IsComplete ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Status.Text = item.IsProcessing ? item.ExtractionText : item.ExtractionStatus == "error"
                ? "解壓縮失敗，原始檔案仍可開啟。" : item.Uploading ? $"做種中 · 已上傳 {DownloadItem.FormatBytes(item.Uploaded)}" : item.StatusText;
            Percent.Text = item.Size > 0 && !item.IsComplete ? $"{item.Percent:0.0}%" : "";
            Progress.Value = item.IsProcessing ? item.Data["progress"]?["extractProgress"]?.GetValue<double>() ?? 0 : item.Percent;
            Progress.IsIndeterminate = item.IsIndeterminate && !item.IsProcessing;
            Progress.Visibility = item.IsProcessing || item.IsComplete || item.Size > 0 || item.IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;
            CloseAfterOpen.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
            Browse.Visibility = item.IsComplete ? Visibility.Visible : Visibility.Collapsed;
            StopSeed.Visibility = item.Uploading ? Visibility.Visible : Visibility.Collapsed;
            if ((changes & DownloadChanges.Content) != 0)
            {
                TitleChanged?.Invoke($"{item.Name} - {item.StatusText}"); LayoutChanged?.Invoke();
            }
            timer.Interval = TimeSpan.FromSeconds(item.CanPause || item.IsProcessing ? 1 : 5);
            if (finished || !windowVisible()) timer.Stop();
        }
        catch (Exception failure) { if (!stopped && !cancelling) ShowError(failure); }
        finally { refreshing = false; }
    }

    private async void PrimaryClick(object sender, RoutedEventArgs args)
    {
        if (item is null) return; Primary.IsEnabled = false;
        try
        {
            if (item.PrimaryAction.Key == "open") { FileActions.Open(item.OpenPath); if (CloseAfterOpen.IsChecked == true) close(); }
            else if (item.CanAct) { await core.SendAsync(HttpMethod.Put, $"tasks/{id}/{item.PrimaryAction.Key}"); await Refresh(); }
        }
        catch (Exception failure) { ShowError(failure); }
        finally { Primary.IsEnabled = item?.CanAct == true; }
    }
    private void BrowseClick(object sender, RoutedEventArgs args)
    {
        try { if (item is not null) FileActions.Reveal(item.FilePath, item.Folder); }
        catch (Exception failure) { ShowError(failure); }
    }
    private async void StopSeedClick(object sender, RoutedEventArgs args)
    {
        try { await core.SendAsync(HttpMethod.Put, $"tasks/{id}/pause"); await Refresh(); }
        catch (Exception failure) { ShowError(failure); }
    }
    private async void CancelClick(object sender, RoutedEventArgs args)
    {
        if (item is null || cancelling) return;
        if (item.IsComplete && !item.IsProcessing && !item.Uploading) { close(); return; }
        cancelling = true;
        Cancel.IsEnabled = false;
        Primary.IsEnabled = false;
        StopSeed.IsEnabled = false;
        timer.Stop();
        try
        {
            await core.SendAsync(HttpMethod.Delete, $"tasks?id={Uri.EscapeDataString(id)}&force=false");
            close();
        }
        catch (Exception failure)
        {
            cancelling = false;
            Cancel.IsEnabled = true;
            StopSeed.IsEnabled = true;
            ShowError(failure);
            timer.Start();
            await Refresh();
        }
    }
    private void CopySource(object sender, RoutedEventArgs args) { if (item is not null) FileActions.Copy(item.Url); }
    private void SaveClosePreference(object sender, RoutedEventArgs args)
    {
        var prefs = UiPreferences.Load(); prefs.CloseProgressAfterOpen = CloseAfterOpen.IsChecked == true; prefs.Save();
    }
    private async void DragFile(UIElement sender, DragStartingEventArgs args)
    {
        if (item is not { IsComplete: true, IsProcessing: false }) { args.Cancel = true; return; }
        var deferral = args.GetDeferral();
        try
        {
            IStorageItem file = Directory.Exists(item.OpenPath) ? await StorageFolder.GetFolderFromPathAsync(item.OpenPath) : await StorageFile.GetFileFromPathAsync(item.OpenPath);
            args.Data.SetStorageItems([file]); args.Data.RequestedOperation = DataPackageOperation.Copy; args.AllowedOperations = DataPackageOperation.Copy;
        }
        catch (Exception failure) { args.Cancel = true; ShowError(failure); }
        finally { deferral.Complete(); }
    }
    public async void UpdatePollingVisibility()
    {
        if (!loaded || stopped || cancelling) return;
        if (!windowVisible()) { timer.Stop(); return; }
        if (timer.IsEnabled || item is { IsComplete: true, IsProcessing: false, Uploading: false }) return;
        timer.Start(); await Refresh();
    }
    public void Stop() { stopped = true; timer.Stop(); }
    private void ShowError(Exception failure) { Error.Message = UserError.Message(failure); Error.Visibility = Visibility.Visible; Error.IsOpen = true; LayoutChanged?.Invoke(); }
    private void HideError(InfoBar sender, InfoBarClosedEventArgs args) { sender.Visibility = Visibility.Collapsed; LayoutChanged?.Invoke(); }
}
