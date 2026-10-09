using System.Text.Json.Nodes;

namespace Harbor.Services;

public static class DownloadRequest
{
    public static JsonObject Create(JsonObject? initial, string url, JsonObject headers, int connections, string folder, string name)
    {
        var request = initial?.DeepClone().AsObject() ?? new JsonObject();
        request["req"] ??= new JsonObject();
        request["opts"] ??= new JsonObject();
        var source = request["req"]!.AsObject();
        var options = request["opts"]!.AsObject();
        source["extra"] ??= new JsonObject();
        options["extra"] ??= new JsonObject();
        source["url"] = url;
        source["extra"]!["header"] = headers;
        options["path"] = folder.Trim();
        options["name"] = name.Trim();
        options["extra"]!["connections"] = connections;
        return request;
    }
}
