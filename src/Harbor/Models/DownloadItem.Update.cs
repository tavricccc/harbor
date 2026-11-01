using System.Text.Json.Nodes;

namespace Harbor.Models;

[Flags]
public enum DownloadChanges
{
    None = 0, Content = 1, Progress = 2
}

public sealed partial class DownloadItem
{
    [Flags]
    private enum Metrics
    {
        None = 0, Downloaded = 1, Uploaded = 2, Speed = 4, Transfer = 8, Status = 16, Remaining = 32
    }

    public DownloadChanges Update(JsonObject data)
    {
        // Progress is the only rapidly changing subtree. A metadata or lifecycle
        // change refreshes all bindings; byte/speed ticks only touch live metrics.
        // Compare each field once: metadata can hold a torrent's whole file list.
        if (!ContentEquals(Data, data))
        {
            Load(data);
            OnPropertyChanged(string.Empty);
            return DownloadChanges.Content | DownloadChanges.Progress;
        }
        var progress = data["progress"];
        if (JsonNode.DeepEquals(Data["progress"], progress))
            return DownloadChanges.None;
        if (ExtractionStatus != (progress?["extractStatus"]?.GetValue<string>() ?? ""))
        {
            Load(data);
            OnPropertyChanged(string.Empty);
            return DownloadChanges.Content | DownloadChanges.Progress;
        }
        var (downloaded, speed, uploaded, uploadSpeed) = (Downloaded, Speed, Uploaded, UploadSpeed);
        var extractProgress = Data["progress"]?["extractProgress"];
        LoadProgress(data);
        // Every changed row raises these each tick, so skip metrics whose inputs stayed the same.
        var changed = Metrics.None;
        if (Downloaded != downloaded)
            changed |= Metrics.Downloaded | Metrics.Transfer | Metrics.Status | Metrics.Remaining;
        if (Speed != speed)
            changed |= Metrics.Speed | Metrics.Remaining;
        if (Uploaded != uploaded)
            changed |= Metrics.Uploaded;
        if (UploadSpeed != uploadSpeed)
            changed |= Metrics.Speed;
        if (IsProcessing && !JsonNode.DeepEquals(extractProgress, progress?["extractProgress"]))
            changed |= Metrics.Transfer | Metrics.Status;
        if ((changed & Metrics.Downloaded) != 0)
        {
            OnPropertyChanged(nameof(Downloaded));
            OnPropertyChanged(nameof(Percent));
        }
        if ((changed & Metrics.Uploaded) != 0)
            OnPropertyChanged(nameof(Uploaded));
        if ((changed & Metrics.Speed) != 0)
        {
            OnPropertyChanged(nameof(Speed));
            OnPropertyChanged(nameof(UploadSpeed));
            OnPropertyChanged(nameof(SpeedText));
            OnPropertyChanged(nameof(RowSpeedText));
        }
        if ((changed & Metrics.Transfer) != 0)
        {
            OnPropertyChanged(nameof(TransferText));
            OnPropertyChanged(nameof(TransferSizeText));
        }
        if ((changed & Metrics.Status) != 0)
        {
            if (IsProcessing)
            {
                OnPropertyChanged(nameof(ExtractionText));
                OnPropertyChanged(nameof(StatusText));
            }
            OnPropertyChanged(nameof(RowProgressText));
        }
        if ((changed & Metrics.Remaining) != 0)
            OnPropertyChanged(nameof(RemainingText));
        if ((changed & (Metrics.Transfer | Metrics.Remaining)) != 0)
            OnPropertyChanged(nameof(SubText));
        return DownloadChanges.Progress;
    }

    private static bool ContentEquals(JsonObject current, JsonObject next)
    {
        if (current.Count != next.Count)
            return false;
        foreach (var (key, value) in current)
            if (key != "progress" && (!next.TryGetPropertyValue(key, out var other) || !JsonNode.DeepEquals(value, other)))
                return false;
        return true;
    }
}
