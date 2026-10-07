using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Models;

public sealed partial class DownloadItem : ObservableObject
{
    public string Id { get; }
    public JsonObject Data { get; private set; }
    public DownloadItem(JsonObject data) { Id = data["id"]!.GetValue<string>(); Data = data; }
    public string Name => Data["name"]!.GetValue<string>();
    public string Status => Data["status"]!.GetValue<string>();
    public bool IsDeferred => Status == "deferred";
    public string DeferredError => Data["error"]?.GetValue<string>() ?? "";
    public DateTimeOffset? ScheduledAt => Data["scheduledAt"] is JsonValue value ? DateTimeOffset.Parse(value.GetValue<string>()) : null;
    public string ExtractionStatus => Data["progress"]?["extractStatus"]?.GetValue<string>() ?? "";
    public string ExtractionText => ExtractionStatus switch { "extracting" => $"解壓縮中 {Data["progress"]?["extractProgress"]}%", "waitingParts" => "等待壓縮檔分卷", "error" => "解壓縮失敗", "done" => "已解壓縮", _ => "" };
    public bool IsProcessing => ExtractionStatus is "extracting" or "waitingParts";
    public bool Uploading => Data["uploading"]?.GetValue<bool>() == true;
    public string StatusText => IsDeferred ? DeferredError.Length > 0 ? "排程失敗" : ScheduledAt is { } time ? $"{time.ToLocalTime():MM/dd HH:mm}" : "稍後下載" : IsProcessing ? ExtractionText : Status switch { "running" => "下載中", "done" => Uploading ? "做種中" : "已完成", "pause" => "已暫停", "error" => "下載失敗", "wait" => "等待中", _ => "準備中" };
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
    public string SizeText => Size > 0 ? FormatBytes(Size) : "大小未知";
    public string TransferText => $"{FormatBytes(Downloaded)} / {SizeText}" + (ExtractionText.Length > 0 ? $" · {ExtractionText}" : "");
    public string TransferSizeText => IsComplete ? SizeText : $"{FormatBytes(Downloaded)} / {SizeText}";
    public string RowProgressText => Status == "running" && Size > 0 ? $"{Percent:0}%" : StatusText;
    public string DetailsText => $"{Name}\n{StatusText} · {TransferText}\n速度：{SpeedText} · 剩餘 {RemainingText}\n{FilePath}\n{Url}" + (DeferredError.Length > 0 ? "\n" + DeferredError : "");
    public long Uploaded => Data["progress"]?["uploaded"]?.GetValue<long>() ?? 0;
    public long UploadSpeed => Data["progress"]?["uploadSpeed"]?.GetValue<long>() ?? 0;
    public string SpeedText => Status == "running" ? FormatBytes(Speed) + "/s" : Uploading ? "↑ " + FormatBytes(UploadSpeed) + "/s" : "—";
    public string RemainingText => Status == "running" && Speed > 0 && Size > Downloaded ? FormatTime((Size - Downloaded) / Speed) : "—";
    public string OpenPath => ExtractionStatus == "done" && !File.Exists(FilePath) && !Directory.Exists(FilePath) ? Folder : FilePath;
    public DownloadAction PrimaryAction => IsDeferred ? new("continue", "立即開始下載", "\uE768") : IsProcessing ? new("none", "正在解壓縮", "\uE895") : IsComplete && OpenPath == Folder ? new("open", "開啟解壓縮資料夾", "\uE8B7") : DownloadPresentation.ForStatus(Status);
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
            if (!string.IsNullOrEmpty(name)) return Path.Combine(Folder, string.IsNullOrEmpty(custom) ? name : custom);
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
    private static string FormatTime(long seconds) => seconds >= 3600 ? $"{seconds / 3600} 小時 {seconds % 3600 / 60} 分" : seconds >= 60 ? $"{seconds / 60} 分 {seconds % 60} 秒" : $"{seconds} 秒";
}
