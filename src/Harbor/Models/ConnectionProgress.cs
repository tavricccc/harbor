using CommunityToolkit.Mvvm.ComponentModel;
using Harbor.Localization;
using System.Diagnostics;

namespace Harbor.Models;

public sealed partial class ConnectionProgress : ObservableObject
{
    [ObservableProperty]
    public partial string Label
    {
        get; set;
    }
    [ObservableProperty]
    public partial string TransferText
    {
        get; set;
    }
    [ObservableProperty]
    public partial string StatusText
    {
        get; set;
    }
    [ObservableProperty]
    public partial string SpeedText
    {
        get; set;
    }
    [ObservableProperty]
    public partial double Percent
    {
        get; set;
    }
    [ObservableProperty]
    public partial bool IsIndeterminate
    {
        get; set;
    }
    private long lastDownloaded;
    private long lastSample;
    private int lastRetries;

    public ConnectionProgress()
    {
        Label = TransferText = StatusText = "";
        SpeedText = "—";
    }

    public void UpdateHttp(HttpConnectionStatistics connection, int index, long now, bool running, long fileSize)
    {
        var downloaded = connection.Downloaded;
        var completed = connection.Completed;
        var failed = connection.Failed;
        var retries = connection.RetryTimes;
        Label = Strings.Format("Details.Connection", index + 1);
        // Work stealing changes each lane's assigned chunks. Its cumulative bytes
        // are a contribution to the whole file, not progress toward a fixed quota.
        Percent = fileSize > 0 ? Math.Min(100, downloaded * 100.0 / fileSize) : 0;
        TransferText = fileSize > 0 ? Strings.Format("Progress.ConnectionShare", $"{Percent:0.0}%") : DownloadItem.FormatBytes(downloaded);
        IsIndeterminate = fileSize <= 0 && running && !completed && !failed;
        StatusText = Strings.Get(completed ? "Status.Completed" : failed ? "Status.Retrying" : running ? "Status.Downloading" : "Status.Paused");
        // HTTP stats expose cumulative bytes. Sample each lane independently.
        var elapsed = lastSample == 0 ? 0 : Stopwatch.GetElapsedTime(lastSample, now).TotalSeconds;
        SpeedText = !running || completed || failed ? "0 B/s" : elapsed > 0 && retries == lastRetries && downloaded >= lastDownloaded
            ? DownloadItem.FormatBytes((long)((downloaded - lastDownloaded) / elapsed)) + "/s" : "—";
        lastSample = now;
        lastDownloaded = downloaded;
        lastRetries = retries;
    }

    public void UpdatePeer(PeerStatistics peer)
    {
        Label = peer.Address;
        var completion = peer.Completion;
        Percent = completion is { } value ? value * 100 : 0;
        IsIndeterminate = false;
        TransferText = completion is { } ratio ? Strings.Format("Progress.PeerCompletion", $"{ratio * 100:0.0}%") : Strings.Get("Status.UnknownSize");
        StatusText = peer.Client;
        SpeedText = DownloadItem.FormatBytes(peer.DownloadSpeed) + "/s";
        lastSample = 0;
    }

    public void ResetSample() => lastSample = 0;
}
