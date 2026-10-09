using System.Text.Json.Nodes;

namespace Harbor.Services;

public static class ConfigJson
{
    public static JsonNode? Get(JsonNode? node, string path)
    {
        foreach (var key in path.Split('.'))
            node = node?[key];
        return node;
    }
    public static void Set(JsonObject root, string path, JsonNode? value)
    {
        var keys = path.Split('.');
        var node = root;
        foreach (var key in keys[..^1])
        {
            node[key] ??= new JsonObject();
            node = node[key]!.AsObject();
        }
        node[keys[^1]] = value;
    }
    public static string[] Lines(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public static JsonArray Array(IEnumerable<string> values) => new(values.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
}
