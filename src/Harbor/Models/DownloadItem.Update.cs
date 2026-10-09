using System.Text.Json.Nodes;

namespace Harbor.Models;

[Flags]
public enum DownloadChanges
{
    None = 0, Content = 1, Progress = 2
}

public sealed partial class DownloadItem
{
    private static readonly string[] progressProperties =
    [
        nameof(Downloaded), nameof(Speed), nameof(Percent), nameof(IsIndeterminate),
        nameof(TransferText), nameof(TransferSizeText), nameof(RowProgressText),
        nameof(DetailsText), nameof(Uploaded), nameof(UploadSpeed), nameof(SpeedText), nameof(RemainingText)
    ];

    public DownloadChanges Update(JsonObject data)
    {
        if (JsonNode.DeepEquals(Data, data))
            return DownloadChanges.None;
        // Progress is the only rapidly changing subtree. A metadata or lifecycle
        // change refreshes all bindings; byte/speed ticks only touch live metrics.
        var contentChanged = Data.Count != data.Count || Data.Any(field => field.Key != "progress" && !JsonNode.DeepEquals(field.Value, data[field.Key]));
        var extractionChanged = ExtractionStatus != (data["progress"]?["extractStatus"]?.GetValue<string>() ?? "");
        Data = data;
        if (contentChanged || extractionChanged)
        {
            OnPropertyChanged(string.Empty);
            return DownloadChanges.Content | DownloadChanges.Progress;
        }
        foreach (var property in progressProperties)
            OnPropertyChanged(property);
        if (IsProcessing)
        {
            OnPropertyChanged(nameof(ExtractionText));
            OnPropertyChanged(nameof(StatusText));
        }
        return DownloadChanges.Progress;
    }
}
