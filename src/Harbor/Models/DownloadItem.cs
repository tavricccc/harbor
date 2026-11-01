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
    } = null!;
    public DownloadItem(JsonObject data)
    {
        Id = data["id"]!.GetValue<string>();
        Load(data);
    }
    // Bindings, filters and sorting read these on every refresh, so each snapshot is parsed once.
    private void Load(JsonObject data)
    {
        LoadProgress(data);
        var meta = data["meta"];
        var progress = data["progress"];
        Name = data["name"]!.GetValue<string>();
        Status = data["status"]!.GetValue<string>();
        DeferredError = data["error"]?.GetValue<string>() ?? "";
        ScheduledAt = data["scheduledAt"] is JsonValue value ? DateTimeOffset.Parse(value.GetValue<string>()) : null;
        ExtractionStatus = progress?["extractStatus"]?.GetValue<string>() ?? "";
        Uploading = data["uploading"]?.GetValue<bool>() == true;
        CreatedAt = DateTimeOffset.Parse(data["createdAt"]!.GetValue<string>());
        Url = meta?["req"]?["url"]?.GetValue<string>() ?? "";
        Folder = meta?["opts"]?["path"]?.GetValue<string>() ?? "";
        Protocol = data["protocol"]?.GetValue<string>().ToUpperInvariant() ?? "";
        Size = meta?["res"]?["size"]?.GetValue<long>() ?? 0;
        var resource = meta?["res"];
        var resourceName = resource?["name"]?.GetValue<string>();
        var file = resource?["files"]?.AsArray().FirstOrDefault();
        var custom = meta?["opts"]?["name"]?.GetValue<string>();
        FilePath = !string.IsNullOrEmpty(resourceName)
            ? Path.Combine(Folder, string.IsNullOrEmpty(custom) ? resourceName : custom)
            : Path.Combine(Folder, file?["path"]?.GetValue<string>() ?? "", string.IsNullOrEmpty(custom) ? file?["name"]?.GetValue<string>() ?? Name : custom);
        FileGlyph = !string.IsNullOrEmpty(resourceName) ? "folder" : DownloadPresentation.FileGlyph(Name);
        primaryAction = null;
    }
    // Progress ticks keep the metadata, so only the live metrics are read again.
    private void LoadProgress(JsonObject data)
    {
        Data = data;
        var progress = data["progress"];
        Downloaded = progress?["downloaded"]?.GetValue<long>() ?? 0;
        Speed = progress?["speed"]?.GetValue<long>() ?? 0;
        Uploaded = progress?["uploaded"]?.GetValue<long>() ?? 0;
        UploadSpeed = progress?["uploadSpeed"]?.GetValue<long>() ?? 0;
    }
    public string Name { get; private set; } = "";
    public string Status { get; private set; } = "";
    public bool IsDeferred => Status == "deferred";
    public string DeferredError { get; private set; } = "";
    public DateTimeOffset? ScheduledAt { get; private set; }
    public string ExtractionStatus { get; private set; } = "";
    public string ExtractionText => ExtractionStatus switch { "extracting" => Strings.Format("Status.ExtractProgress", Data["progress"]?["extractProgress"]), "waitingParts" => Strings.Get("Status.WaitingParts"), "error" => Strings.Get("Status.ExtractFailed"), "done" => Strings.Get("Status.Extracted"), _ => "" };
    public bool IsProcessing => ExtractionStatus is "extracting" or "waitingParts";
    public bool Uploading { get; private set; }
    public string StatusText => IsDeferred ? DeferredError.Length > 0 ? Strings.Get("Status.ScheduleFailed") : ScheduledAt is { } time ? time.ToLocalTime().ToString("g") : Strings.Get("Schedule.Later") : IsProcessing ? ExtractionText : Status switch { "running" => Strings.Get("Status.Downloading"), "done" => Uploading ? Strings.Get("Status.Seeding") : Strings.Get("Status.Completed"), "pause" => Strings.Get("Status.Paused"), "error" => Strings.Get("Status.DownloadFailed"), "wait" => Strings.Get("Status.Waiting"), _ => Strings.Get("Status.Preparing") };
    public DateTimeOffset CreatedAt { get; private set; }
    public string Url { get; private set; } = "";
    public string Folder { get; private set; } = "";
    public string Protocol { get; private set; } = "";
    public long Size { get; private set; }
    public long Downloaded { get; private set; }
    public long Speed { get; private set; }
    public double Percent => Status == "done" ? 100 : Size > 0 ? Math.Min(100, Downloaded * 100.0 / Size) : 0;
    public bool IsIndeterminate => Size <= 0 && Status == "running";
    public bool CanPause => Status is "running" or "wait" or "ready" || Uploading;
    public bool CanResume => IsDeferred || Status is "pause" or "error";
    public bool IsComplete => Status == "done";
    public string SizeText => Size > 0 ? FormatBytes(Size) : Strings.Get("Status.UnknownSize");
    public string TransferText => $"{FormatBytes(Downloaded)} / {SizeText}" + (ExtractionText.Length > 0 ? $" · {ExtractionText}" : "");
    public string TransferSizeText => IsComplete ? SizeText : $"{FormatBytes(Downloaded)} / {SizeText}";
    public string RowProgressText => Status == "running" && Size > 0 ? $"{Percent:0}%" : StatusText;
    // The row's second line: what has arrived, and while running how long is left; a deferred task shows its link or why it failed.
    public string SubText => IsDeferred ? DeferredError.Length > 0 ? DeferredError : Url : Status == "running" && RemainingText != "—" ? $"{TransferSizeText} · {RemainingText}" : TransferSizeText;
    public long Uploaded { get; private set; }
    public long UploadSpeed { get; private set; }
    public string SpeedText => Status == "running" ? FormatBytes(Speed) + "/s" : Uploading ? "↑ " + FormatBytes(UploadSpeed) + "/s" : "—";
    // The list leaves the speed column empty while nothing moves.
    public string RowSpeedText => Status == "running" || Uploading ? SpeedText : "";
    public string RemainingText => Status == "running" && Speed > 0 && Size > Downloaded ? FormatTime((Size - Downloaded) / Speed) : "—";
    // Opening checks the disk each time; the row action keeps its check until the next snapshot.
    public string OpenPath => ExtractionStatus == "done" && !File.Exists(FilePath) && !Directory.Exists(FilePath) ? Folder : FilePath;
    private DownloadAction? primaryAction;
    public DownloadAction PrimaryAction => primaryAction ??= IsDeferred ? new("continue", Strings.Get("Downloads.StartNow"), "play") : IsProcessing ? new("none", Strings.Get("Status.Extracting"), "archive") : IsComplete && OpenPath == Folder ? new("open", Strings.Get("Downloads.OpenExtracted"), "folder") : DownloadPresentation.ForStatus(Status);
    public string PrimaryActionLabel => PrimaryAction.Label;
    public string PrimaryActionGlyph => PrimaryAction.Glyph;
    public string FileGlyph { get; private set; } = "file";
    public bool CanEditSource => Protocol == "HTTP" && (Status is "pause" or "error");
    public bool CanReveal => !IsDeferred;
    public bool CanAct => PrimaryAction.Key != "none";
    public string FilePath { get; private set; } = "";
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
