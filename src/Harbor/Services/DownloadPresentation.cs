using Harbor.Localization;
namespace Harbor.Services;

public sealed record DownloadAction(string Key, string Label, string Glyph);

public static class DownloadPresentation
{
    public static DownloadAction ForStatus(string status) => status switch
    {
        "done" => new("open", Strings.Get("Downloads.OpenFile"), "\uE8E5"),
        "pause" => new("continue", Strings.Get("Downloads.Resume"), "\uE768"),
        "error" => new("continue", Strings.Get("Downloads.Retry"), "\uE72C"),
        "running" or "wait" or "ready" => new("pause", Strings.Get("Downloads.Pause"), "\uE769"),
        _ => new("none", Strings.Get("Downloads.Select"), "\uE896")
    };

    public static string FileGlyph(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "\uF012",
        ".exe" or ".msi" or ".msix" => "\uE713",
        ".mp3" or ".flac" or ".wav" or ".m4a" => "\uE8D6",
        ".mp4" or ".mkv" or ".webm" or ".mov" => "\uE714",
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" => "\uE91B",
        _ => "\uE8A5"
    };
}
