using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Models;
using Gopeed_Native.Services;
using System.Net.Http;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Gopeed_Native.Views;

public sealed partial class DownloadProgressPage : Page
{
    private readonly CoreClient core;
    private readonly string id;
    private readonly Action close;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DownloadItem? item;
    private bool refreshing;
    public event Action? LayoutChanged;
    public event Action<string>? TitleChanged;

    public DownloadProgressPage(CoreClient core, string id, Action close)
    {
        InitializeComponent();
        this.core = core; this.id = id; this.close = close;
        CloseAfterOpen.IsChecked = UiPreferences.Load().CloseProgressAfterOpen;
        Body.SizeChanged += (_, _) => LayoutChanged?.Invoke();
        timer.Tick += async (_, _) => await Refresh();
        Loaded += async (_, _) => { timer.Start(); await Refresh(); };
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
        if (refreshing) return; refreshing = true;
        try
        {
            item = new DownloadItem((await core.GetAsync("tasks/" + id))!.AsObject());
            Primary.Content = item.PrimaryAction.Label;
            Primary.IsEnabled = item.CanAct;
            FileName.Text = item.Name; FileName.CanDrag = item.IsComplete && !item.IsProcessing;
            KindLabel.Text = item.Url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || item.Url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ? "BT" : "檔案";
            ToolTipService.SetToolTip(FileName, item.Name);
            Folder.Text = $"存到：{item.Folder}"; ToolTipService.SetToolTip(Folder, item.Folder); Source.Text = item.Url;
            Transfer.Text = item.TransferSizeText;
            Speed.Text = $"{(item.Uploading ? "上傳" : "下載")}：{item.SpeedText}";
            Remaining.Text = item.RemainingText == "—" ? item.Status == "pause" ? "已暫停" : "計算中" : item.RemainingText;
            var finished = item.IsComplete && !item.IsProcessing && !item.Uploading;
            Primary.Style = (Style)Application.Current.Resources[finished || item.Status == "error" ? "AccentButtonStyle" : "DefaultButtonStyle"];
            Speed.Visibility = finished ? Visibility.Collapsed : Visibility.Visible;
            Remaining.Visibility = item.IsComplete ? Visibility.Collapsed : Visibility.Visible;
            Metrics.ColumnDefinitions[1].Width = finished ? new GridLength(0) : GridLength.Auto;
            Metrics.ColumnDefinitions[2].Width = item.IsComplete ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Status.Text = item.IsProcessing ? item.ExtractionText : item.ExtractionStatus == "error"
                ? "解壓縮失敗，原始檔案仍可開啟。" : item.Uploading ? $"做種中 · 已上傳 {DownloadItem.FormatBytes(item.Uploaded)}" : item.StatusText;
            Percent.Text = item.Size > 0 && !item.IsComplete ? $"{item.Percent:0.0}%" : "";
            Progress.Value = item.IsProcessing ? item.Data["progress"]?["extractProgress"]?.GetValue<double>() ?? 0 : item.Percent;
            Progress.Visibility = item.IsProcessing || item.IsComplete || item.Size > 0 ? Visibility.Visible : Visibility.Collapsed;
            CloseAfterOpen.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
            Browse.Visibility = item.IsComplete ? Visibility.Visible : Visibility.Collapsed;
            StopSeed.Visibility = item.Uploading ? Visibility.Visible : Visibility.Collapsed;
            TitleChanged?.Invoke($"{item.Name} - {item.StatusText}"); LayoutChanged?.Invoke();
            if (finished) timer.Stop();
        }
        catch (Exception failure) { ShowError(failure); }
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
    private void CloseClick(object sender, RoutedEventArgs args) => close();
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
    public void Stop() => timer.Stop();
    private void ShowError(Exception failure) { Error.Message = UserError.Message(failure); Error.Visibility = Visibility.Visible; Error.IsOpen = true; LayoutChanged?.Invoke(); }
    private void HideError(InfoBar sender, InfoBarClosedEventArgs args) { sender.Visibility = Visibility.Collapsed; LayoutChanged?.Invoke(); }
}
