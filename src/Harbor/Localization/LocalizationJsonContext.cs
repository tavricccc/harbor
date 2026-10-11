using System.Text.Json.Serialization;

namespace Harbor.Localization;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LanguageOption[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocalizationJsonContext : JsonSerializerContext;
