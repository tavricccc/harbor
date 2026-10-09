using Harbor.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Models;

public sealed partial class DownloadItem : ObservableObject
{
    public string Id
    {
        get;
    }
    public JsonObject Data
    {
        get; private set;
    }
    public DownloadItem(JsonObject data)
    {
        Id = data["id"]!.GetValue<string>();
        Data = data;
    }
    public string Name => Data["name"]!.GetValue<string>();
    public string Status => Data["status"]!.GetValue<string>();
    public bool IsDeferred => Status == "deferred";
    public string DeferredError => Data["error"]?.GetValue<string>() ?? "";
    public DateTimeOffset? ScheduledAt => Data["scheduledAt"] is JsonValue value ? DateTimeOffset.Parse(value.GetValue<string>()) : null;
    public string ExtractionStatus => Data["progress"]?["extractStatus"]?.GetValue<string>() ?? "";
    public string ExtractionText => ExtractionStatus switch { "extracting" => Strings.Format("Status.ExtractProgress", Data["progress"]?["extractProgress"]), "waitingParts" => Strings.Get("Status.WaitingParts"), "error" => Strings.Get("Status.ExtractFailed"), "done" => Strings.Get("Status.Extracted"), _ => "" };
    public bool IsProcessing => ExtractionStatus is "extracting" or "waitingParts";
    public bool Uploading => Data["uploading"]?.GetValue<bool>() == true;
    public string StatusText => IsDeferred ? DeferredError.Length > 0 ? Strings.Get("Status.ScheduleFailed") : ScheduledAt is { } time ? time.ToLocalTime().ToString("g") : Strings.Get("Schedule.Later") : IsProcessing ? ExtractionText : Status switch { "running" => Strings.Get("Status.Downloading"), "done" => Uploading ? Strings.Get("Status.Seeding") : Strings.Get("Status.Completed"), "pause" => Strings.Get("Status.Paused"), "error" => Strings.Get("Status.DownloadFailed"), "wait" => Strings.Get("Status.Waiting"), _ => Strings.Get("Status.Preparing") };
    public DateTimeOffset CreatedAt => DateTimeOffset.Parse(Data["createdAt"]!.GetValue<string>());
    public string Url => Data["meta"]?["req"]?["url"]?.GetValue<string>() ?? "";
    public string Folder => Data["meta"]?["opts"]?["path"]?.GetValue<string>() ?? "";
    public string Protocol => Data["protocol"]?.GetValue<string>().ToUpperInvariant() ?? "";
    public long Size => Data["meta"]?["res"]?["size"]?.GetValue<long>() ?? 0;
    public long Downloaded => Data["progress"]?["downloaded"]?.GetValue<long>() ?? 0;
    public long Speed => Data["progress"]?["speed"]?.GetValue<long>() ?? 0;
    public double Percent => Status == "done" ? 100 : Size > 0 ? Math.Min(100, Downloaded * 100.0 / Size) : 0;
    public bool IsIndeterminate => Size <= 0 && Status == "running";
    public bool CanPause => Status is "running" or "wait" or "ready" || Data["uploading"]?.GetValue<bool>() == true;
    public bool CanResume => IsDeferred || Status is "pause" or "error";
    public bool IsComplete => Status == "done";
    public string SizeText => Size > 0 ? FormatBytes(Size) : Strings.Get("Status.UnknownSize");
    public string TransferText => $"{FormatBytes(Downloaded)} / {SizeText}" + (ExtractionText.Length > 0 ? $" · {ExtractionText}" : "");
    public string TransferSizeText => IsComplete ? SizeText : $"{FormatBytes(Downloaded)} / {SizeText}";
    public string RowProgressText => Status == "running" && Size > 0 ? $"{Percent:0}%" : StatusText;
    public string DetailsText => Strings.Format("Downloads.DetailsText", Name, StatusText, TransferText, SpeedText, RemainingText, FilePath, Url) + (DeferredError.Length > 0 ? "\n" + DeferredError : "");
    public long Uploaded => Data["progress"]?["uploaded"]?.GetValue<long>() ?? 0;
    public long UploadSpeed => Data["progress"]?["uploadSpeed"]?.GetValue<long>() ?? 0;
    public string SpeedText => Status == "running" ? FormatBytes(Speed) + "/s" : Uploading ? "↑ " + FormatBytes(UploadSpeed) + "/s" : "—";
    public string RemainingText => Status == "running" && Speed > 0 && Size > Downloaded ? FormatTime((Size - Downloaded) / Speed) : "—";
    public string OpenPath => ExtractionStatus == "done" && !File.Exists(FilePath) && !Directory.Exists(FilePath) ? Folder : FilePath;
    public DownloadAction PrimaryAction => IsDeferred ? new("continue", Strings.Get("Downloads.StartNow"), "\uE768") : IsProcessing ? new("none", Strings.Get("Status.Extracting"), "\uE895") : IsComplete && OpenPath == Folder ? new("open", Strings.Get("Downloads.OpenExtracted"), "\uE8B7") : DownloadPresentation.ForStatus(Status);
    public string PrimaryActionLabel => PrimaryAction.Label;
    public string PrimaryActionGlyph => PrimaryAction.Glyph;
    public string FileGlyph => Data["meta"]?["res"]?["name"]?.GetValue<string>() is { Length: > 0 } ? "\uE8B7" : DownloadPresentation.FileGlyph(Name);
    public bool CanEditSource => Protocol == "HTTP" && (Status is "pause" or "error");
    public bool CanReveal => !IsDeferred;
    public bool CanAct => PrimaryAction.Key != "none";
    public string FilePath
    {
        get
        {
            var resource = Data["meta"]?["res"];
            var name = resource?["name"]?.GetValue<string>();
            var file = resource?["files"]?.AsArray().FirstOrDefault();
            var custom = Data["meta"]?["opts"]?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(name))
                return Path.Combine(Folder, string.IsNullOrEmpty(custom) ? name : custom);
            return Path.Combine(Folder, file?["path"]?.GetValue<string>() ?? "", string.IsNullOrEmpty(custom) ? file?["name"]?.GetValue<string>() ?? Name : custom);
        }
    }
    public override string ToString() => Name;
    public static string FormatBytes(long value) => value switch
    {
        >= 1L << 30 => $"{value / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{value / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{value / 1024.0:0.0} KB",
        _ => $"{value} B"
    };
    private static string FormatTime(long seconds) => seconds >= 3600 ? Strings.Format("Duration.HoursMinutes", seconds / 3600, seconds % 3600 / 60) : seconds >= 60 ? Strings.Format("Duration.MinutesSeconds", seconds / 60, seconds % 60) : Strings.Format("Duration.Seconds", seconds);
}
