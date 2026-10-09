using Harbor.Localization;
using Harbor.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

public sealed partial class DownloadProgressPage
{
    private void UpdatePresentation(DownloadItem task, DownloadChanges changes)
    {
        Primary.Content = task.PrimaryAction.Label;
        Primary.IsEnabled = task.CanAct && !cancelling;
        FileName.Text = task.Name;
        FileName.CanDrag = task.IsComplete && !task.IsProcessing;
        KindLabel.Text = task.Protocol == "BT" ? "BT" : Strings.Get("Common.File");
        ToolTipService.SetToolTip(FileName, task.Name);
        Folder.Text = Strings.Format("Progress.SaveLocation", task.Folder);
        ToolTipService.SetToolTip(Folder, task.Folder);
        Source.Text = task.Url;
        ToolTipService.SetToolTip(SourceLink, task.Url);
        SourceLink.Visibility = task.Url.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Transfer.Text = task.TransferSizeText;
        Speed.Text = Strings.Format("Progress.Speed", task.Uploading ? Strings.Get("Common.Upload") : Strings.Get("Common.Download"), task.SpeedText);
        Remaining.Text = task.RemainingText == "—" ? task.Status == "running" ? Strings.Get("Progress.Calculating") : task.StatusText : task.RemainingText;

        var finished = task.IsComplete && !task.IsProcessing && !task.Uploading;
        Cancel.Content = Strings.Get(finished ? "Common.Close" : "Common.Cancel");
        Cancel.IsEnabled = !cancelling;
        ToolTipService.SetToolTip(Cancel, Strings.Get(finished ? "Progress.CloseWindow" : "Progress.CancelHint"));
        Primary.Style = (Style)Application.Current.Resources[finished || task.Status == "error" ? "AccentButtonStyle" : "DefaultButtonStyle"];
        Speed.Visibility = finished ? Visibility.Collapsed : Visibility.Visible;
        Remaining.Visibility = task.IsComplete ? Visibility.Collapsed : Visibility.Visible;
        Metrics.ColumnDefinitions[1].Width = finished ? new GridLength(0) : GridLength.Auto;
        Metrics.ColumnDefinitions[2].Width = task.IsComplete ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Status.Text = task.IsProcessing ? task.ExtractionText : task.ExtractionStatus == "error"
            ? Strings.Get("Progress.ExtractFailed") : task.Uploading ? Strings.Format("Progress.Seeding", DownloadItem.FormatBytes(task.Uploaded)) : task.StatusText;
        Percent.Text = task.Size > 0 && !task.IsComplete ? $"{task.Percent:0.0}%" : "";
        Progress.Value = task.IsProcessing ? task.Data["progress"]?["extractProgress"]?.GetValue<double>() ?? 0 : task.Percent;
        Progress.IsIndeterminate = task.IsIndeterminate && !task.IsProcessing;
        Progress.Visibility = task.IsProcessing || task.IsComplete || task.Size > 0 || task.IsIndeterminate ? Visibility.Visible : Visibility.Collapsed;
        CloseAfterOpen.Visibility = finished ? Visibility.Visible : Visibility.Collapsed;
        Browse.Visibility = task.IsComplete ? Visibility.Visible : Visibility.Collapsed;
        StopSeed.Visibility = task.Uploading ? Visibility.Visible : Visibility.Collapsed;
        if ((changes & DownloadChanges.Content) != 0)
        {
            TitleChanged?.Invoke($"{task.Name} - {task.StatusText}");
            LayoutChanged?.Invoke();
        }
    }
}
