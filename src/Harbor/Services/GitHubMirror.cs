using System.Text.Json.Nodes;

namespace Harbor.Services;

public static class GitHubMirror
{
    public static string Apply(string source, JsonNode config)
    {
        if (config["extra"]?["githubMirror"]?["enabled"]?.GetValue<bool>() != true)
            return source;
        var mirrors = config["extra"]?["githubMirror"]?["mirrors"]?.AsArray();
        var mirror = mirrors?.FirstOrDefault(x => x?["isDeleted"]?.GetValue<bool>() != true);
        if (mirror is null || !Uri.TryCreate(source, UriKind.Absolute, out var uri))
            return source;
        var address = mirror["url"]!.GetValue<string>().TrimEnd('/');
        if (mirror["type"]?.GetValue<string>() == "jsdelivr" && uri.Host == "raw.githubusercontent.com")
        {
            var parts = uri.AbsolutePath.TrimStart('/').Split('/', 4);
            if (parts.Length == 4)
                return $"{address}/gh/{parts[0]}/{parts[1]}@{parts[2]}/{parts[3]}";
        }
        else if (mirror["type"]?.GetValue<string>() == "ghProxy" && uri.Host is "github.com" or "raw.githubusercontent.com")
            return address + "/" + source;
        return source;
    }
}
