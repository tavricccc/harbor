using Harbor.Localization;
namespace Harbor.Services;

/// <summary>A row action; Glyph names a Harbor.Controls.Icon.</summary>
public sealed record DownloadAction(string Key, string Label, string Glyph);

public static class DownloadPresentation
{
    public static DownloadAction ForStatus(string status) => status switch
    {
        "done" => new("open", Strings.Get("Downloads.OpenFile"), "open"),
        "pause" => new("continue", Strings.Get("Downloads.Resume"), "play"),
        "error" => new("continue", Strings.Get("Downloads.Retry"), "retry"),
        "running" or "wait" or "ready" => new("pause", Strings.Get("Downloads.Pause"), "pause"),
        _ => new("none", Strings.Get("Downloads.Select"), "check")
    };

    public static string FileGlyph(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "archive",
        ".exe" or ".msi" or ".msix" => "app",
        ".mp3" or ".flac" or ".wav" or ".m4a" => "music",
        ".mp4" or ".mkv" or ".webm" or ".mov" => "video",
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" => "image",
        _ => "file"
    };
}
