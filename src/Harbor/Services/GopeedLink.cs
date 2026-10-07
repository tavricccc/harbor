using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Harbor.Services;

public sealed record GopeedLink(string Route, JsonObject? Parameters)
{
    public static string? FromCommandLine(string arguments)
    {
        var match = Regex.Match(arguments, @"gopeed:[^\s""]+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }

    public static GopeedLink Parse(string value)
    {
        var uri = new Uri(value);
        if (uri.Scheme != "gopeed") throw new FormatException("這不是 Gopeed 連結。");
        var route = uri.AbsolutePath.Trim('/');
        JsonObject? parameters = null;
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair[0] != "params" || pair.Length != 2 || pair[1].Length == 0) continue;
            var encoded = Uri.UnescapeDataString(pair[1]).Replace(' ', '+').Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            parameters = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)))!.AsObject();
        }
        return new(route, parameters);
    }
}
