using System.Globalization;
using System.Text.Json;

namespace Harbor.Localization;

public static class Strings
{
    public static IReadOnlyList<string> SupportedLanguages { get; } = ["en-US", "zh-TW", "zh-CN"];
    public static string Language { get; private set; } = "en-US";
    private static Dictionary<string, string> catalog = Load(Language);

    public static void Initialize(string preference, IEnumerable<string> systemLanguages)
    {
        Language = ResolveLanguage(preference, systemLanguages);
        catalog = Load(Language);
        var culture = CultureInfo.GetCultureInfo(Language);
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture = culture;
    }

    public static string ResolveLanguage(string preference, IEnumerable<string> systemLanguages)
    {
        if (preference.Length > 0)
        {
            if (!SupportedLanguages.Contains(preference)) throw new ArgumentException("Unsupported language", nameof(preference));
            return preference;
        }
        foreach (var language in systemLanguages)
        {
            var parts = language.ToLowerInvariant().Split('-');
            if (parts[0] == "en") return "en-US";
            if (parts[0] == "zh")
                return parts.Intersect(["hant", "tw", "hk", "mo"]).Any() ? "zh-TW" : "zh-CN";
        }
        return "en-US";
    }

    public static string Get(string key) => catalog[key];
    public static string Format(string key, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(Strings).Assembly.GetManifestResourceStream($"Harbor.Localization.{language}.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
