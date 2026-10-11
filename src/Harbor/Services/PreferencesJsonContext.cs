using System.Text.Json.Serialization;

namespace Harbor.Services;

[JsonSerializable(typeof(UiPreferences))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class PreferencesJsonContext : JsonSerializerContext;
