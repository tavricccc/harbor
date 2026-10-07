using System.Text.Json.Nodes;

namespace Harbor.Services;

public static class HttpHeaders
{
    public static JsonObject Parse(string text)
    {
        var headers = new JsonObject();
        // WinUI TextBox stores line breaks as CR, including pasted LF text.
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf(':');
            if (separator < 1) throw new FormatException("HTTP 標頭請使用「名稱: 值」格式。");
            headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        return headers;
    }
}
