using System.Text.Json.Nodes;

namespace Harbor.Services;

public sealed record AvailableUpdate(string Version, string Url, string Installer);
public static class UpdateService
{
    public static async Task<AvailableUpdate?> CheckAsync(CoreClient core)
    {
        var releases = JsonNode.Parse(await core.FetchTextAsync("https://api.github.com/repos/tavricccc/harbor/releases?per_page=10"))!.AsArray();
        var current = typeof(App).Assembly.GetName().Version!;
        foreach (var release in releases.Where(x => x?["draft"]?.GetValue<bool>() != true))
        {
            var tag = release!["tag_name"]!.GetValue<string>();
            if (Version.TryParse(tag.TrimStart('v'), out var version) && version > current) { var asset = release["assets"]!.AsArray().FirstOrDefault(x => x?["name"]?.GetValue<string>() == $"Harbor-Setup-{version.ToString(3)}-x64.exe"); if (asset is not null) return new(tag, release["html_url"]!.GetValue<string>(), asset["browser_download_url"]!.GetValue<string>()); }
        }
        return null;
    }
    public static async Task PromptAsync(CoreClient core, AvailableUpdate update, Microsoft.UI.Xaml.XamlRoot root)
    {
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog { Title = $"Harbor {update.Version}", Content = "下載安裝程式後即可更新，原有下載與設定會保留。", PrimaryButtonText = "下載安裝程式", SecondaryButtonText = "版本說明", CloseButtonText = "稍後" };
        var result = await NativeDialogs.ShowAsync(dialog, root);
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Secondary) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update.Url) { UseShellExecute = true });
        else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary) { var config = (await core.GetAsync("config"))!; ((App)Microsoft.UI.Xaml.Application.Current).OpenDownloadWindow(new JsonObject { ["req"] = new JsonObject { ["url"] = GitHubMirror.Apply(update.Installer, config) }, ["opts"] = new JsonObject { ["path"] = Path.Combine(CoreClient.DataDirectory, "Updates"), ["name"] = Path.GetFileName(new Uri(update.Installer).LocalPath) } }); }
    }
}
