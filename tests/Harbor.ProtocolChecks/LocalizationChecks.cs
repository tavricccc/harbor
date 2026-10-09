using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Harbor.Localization;
using Harbor.Services;

internal static class LocalizationChecks
{
    public static void Run()
    {
        foreach (var (system, expected) in new[] { ("en-GB", "en-US"), ("zh-Hant", "zh-TW"), ("zh-Hant-HK", "zh-TW"), ("zh-Hant-CN", "zh-TW"), ("zh-TW", "zh-TW"), ("zh-MO", "zh-TW"), ("zh-Hans", "zh-CN"), ("zh-Hans-HK", "zh-CN"), ("zh-SG", "zh-CN"), ("zh-CN", "zh-CN"), ("de-DE", "en-US") })
            if (Strings.ResolveLanguage("", [system]) != expected) throw new Exception($"Wrong language for {system}");
        if (Strings.ResolveLanguage("zh-CN", ["en-US"]) != "zh-CN") throw new Exception("Language preference ignored");
        if (Strings.ResolveLanguage("", ["de-DE", "zh-TW"]) != "zh-TW") throw new Exception("Windows language priority ignored");

        Dictionary<string, string>? baseline = null;
        foreach (var language in Strings.SupportedLanguages)
        {
            using var stream = typeof(Strings).Assembly.GetManifestResourceStream($"Harbor.Localization.{language}.json")!;
            var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            baseline ??= catalog;
            if (!baseline.Keys.Order().SequenceEqual(catalog.Keys.Order())) throw new Exception($"Missing translation in {language}");
            Strings.Initialize(language, []);
            if (CultureInfo.CurrentUICulture.Name != language) throw new Exception("UI culture was not applied");
            if (UserError.Message(new DownloadApiException("schedule must be in the future")) != Strings.Get("Errors.FutureTime")) throw new Exception("Schedule validation was not localized");
            if (UserError.Message(new IOException(Strings.Get("Errors.CoreStart"))) != Strings.Get("Errors.CoreStart")) throw new Exception("Localized startup error was lost");
            foreach (var (key, value) in catalog)
            {
                if (string.IsNullOrWhiteSpace(value)) throw new Exception($"Empty translation: {language}/{key}");
                var format = CompositeFormat.Parse(value);
                if (!Placeholders(value).SequenceEqual(Placeholders(baseline[key]))) throw new Exception($"Format mismatch: {language}/{key}");
                Strings.Format(key, Enumerable.Repeat<object>(42, format.MinimumArgumentCount).ToArray());
                if (language == "en-US" && value.Any(c => c is >= '\u4e00' and <= '\u9fff')) throw new Exception($"Untranslated English: {key}");
            }
        }
        if (Strings.Get("Common.Cancel") != "取消") throw new Exception("Simplified Chinese catalog not loaded");
        Strings.Initialize("en-US", []);
        if (Strings.Get("Common.Settings") != "Settings" || Strings.Format("Remove.Confirm", 2) != "Remove downloads (2)?") throw new Exception("English catalog not loaded");
        Console.WriteLine($"Localization checks passed: Windows language selection, saved overrides, three embedded catalogs, {baseline!.Count} keys and format arguments.");
    }

    private static IEnumerable<string> Placeholders(string value) => Regex.Matches(value, @"\{(\d+)(?:[^{}]*)\}").Select(match => match.Groups[1].Value).Distinct().Order();
}
