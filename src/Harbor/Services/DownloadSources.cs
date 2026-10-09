using System.Text.RegularExpressions;

namespace Harbor.Services;

public static class DownloadSources
{
    public static bool IsTorrent(string value) => value.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.AbsolutePath : value).EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
    public static bool IsSupported(string value) => File.Exists(value) && value.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)
        || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "magnet" or "ed2k" or "http" or "https";
    public static string? FromArguments(string arguments)
    {
        foreach (Match match in Regex.Matches(arguments, "\"([^\"]+)\"|(\\S+)"))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (value.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) && File.Exists(value))
                return value;
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "magnet" or "ed2k" or "http" or "https")
                return value;
        }
        return null;
    }
}
