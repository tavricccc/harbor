using CommunityToolkit.Mvvm.ComponentModel;
using Harbor.Localization;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Harbor.Models;

public sealed partial class ConnectionProgress : ObservableObject
{
    [ObservableProperty] private string label = "";
    [ObservableProperty] private string transferText = "";
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private string speedText = "—";
    [ObservableProperty] private double percent;
    [ObservableProperty] private bool isIndeterminate;
    private long lastDownloaded;
    private long lastSample;
    private int lastRetries;

    public void UpdateHttp(JsonNode connection, int index, long now, bool running)
    {
        var downloaded = connection["downloaded"]!.GetValue<long>();
        var total = connection["total"]!.GetValue<long>();
        var completed = connection["completed"]!.GetValue<bool>();
        var failed = connection["failed"]!.GetValue<bool>();
        var retries = connection["retryTimes"]!.GetValue<int>();
        Label = Strings.Format("Details.Connection", index + 1);
        TransferText = total > 0 ? $"{DownloadItem.FormatBytes(downloaded)} / {DownloadItem.FormatBytes(total)}" : DownloadItem.FormatBytes(downloaded);
        Percent = completed ? 100 : total > 0 ? Math.Min(100, downloaded * 100.0 / total) : 0;
        IsIndeterminate = total <= 0 && running && !completed && !failed;
        StatusText = Strings.Get(completed ? "Status.Completed" : failed ? "Status.Retrying" : running ? "Status.Downloading" : "Status.Paused");
        // HTTP stats expose cumulative bytes. Sample each lane independently.
        var elapsed = lastSample == 0 ? 0 : Stopwatch.GetElapsedTime(lastSample, now).TotalSeconds;
        SpeedText = !running || completed || failed ? "0 B/s" : elapsed > 0 && retries == lastRetries && downloaded >= lastDownloaded
            ? DownloadItem.FormatBytes((long)((downloaded - lastDownloaded) / elapsed)) + "/s" : "—";
        lastSample = now; lastDownloaded = downloaded; lastRetries = retries;
    }

    public void UpdatePeer(JsonNode peer)
    {
        Label = peer["address"]!.GetValue<string>();
        var completion = peer["completion"]?.GetValue<double>();
        Percent = completion is { } value ? value * 100 : 0;
        IsIndeterminate = false;
        TransferText = completion is { } ratio ? Strings.Format("Progress.PeerCompletion", $"{ratio * 100:0.0}%") : Strings.Get("Status.UnknownSize");
        StatusText = peer["client"]!.GetValue<string>();
        SpeedText = DownloadItem.FormatBytes(peer["downloadSpeed"]!.GetValue<long>()) + "/s";
        lastSample = 0;
    }

    public void ResetSample() => lastSample = 0;
}
