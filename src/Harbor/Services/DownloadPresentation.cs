namespace Harbor.Services;

public sealed record DownloadAction(string Key, string Label, string Glyph);

public static class DownloadPresentation
{
    public static DownloadAction ForStatus(string status) => status switch
    {
        "done" => new("open", "開啟檔案", "\uE8E5"),
        "pause" => new("continue", "繼續下載", "\uE768"),
        "error" => new("continue", "重試下載", "\uE72C"),
        "running" or "wait" or "ready" => new("pause", "暫停下載", "\uE769"),
        _ => new("none", "選取下載", "\uE896")
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
