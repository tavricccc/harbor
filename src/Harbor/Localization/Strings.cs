using System.Globalization;
using System.Text.Json;

namespace Harbor.Localization;

public static class Strings
{
    public static IReadOnlyList<LanguageOption> Languages { get; } = ReadResource<LanguageOption[]>("languages");
    public static IReadOnlyList<string> SupportedLanguages { get; } = Languages.Select(language => language.Id).ToArray();
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
            if (!SupportedLanguages.Contains(preference))
                throw new ArgumentException("Unsupported language", nameof(preference));
            return preference;
        }
        foreach (var language in systemLanguages)
        {
            var parts = language.ToLowerInvariant().Split('-');
            if (parts[0] == "en")
                return "en-US";
            if (parts[0] == "zh")
            {
                if (parts.Contains("hant"))
                    return "zh-TW";
                if (parts.Contains("hans"))
                    return "zh-CN";
                return parts.Intersect(["tw", "hk", "mo"]).Any() ? "zh-TW" : "zh-CN";
            }
        }
        return "en-US";
    }

    public static string Get(string key) => catalog[key];
    public static string Format(string key, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static Dictionary<string, string> Load(string language)
        => ReadResource<Dictionary<string, string>>(language);

    private static T ReadResource<T>(string name)
    {
        using var stream = typeof(Strings).Assembly.GetManifestResourceStream($"Harbor.Localization.{name}.json")!;
        return JsonSerializer.Deserialize<T>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
