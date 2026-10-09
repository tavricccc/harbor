using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.Models;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed partial class DownloadForm
{
    private JsonObject BuildRequest(string url)
    {
        var request = DownloadRequest.Create(initial, url, HttpHeaders.Parse(Headers.Text), (int)Connections.Value, Destination.Text, FileName.Text);
        requestOptions.Apply(request);
        return request;
    }
    public async Task<bool> SubmitAsync(bool defer = false, DateTimeOffset? startAt = null)
    {
        bool complete = false;
        SetBusy(true); Message.IsOpen = false; Message.Visibility = Visibility.Collapsed;
        try
        {
            var links = Links.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (links.Length == 0) throw new FormatException(Strings.Get("Errors.EnterLink"));
            if (double.IsNaN(Connections.Value)) throw new FormatException(Strings.Get("Errors.EnterConnections"));
            if (!Path.IsPathFullyQualified(Destination.Text.Trim())) throw new FormatException(Strings.Get("Errors.SavePath"));
            if (FileName.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new FormatException(Strings.Get("Errors.FileName"));
            Directory.CreateDirectory(Destination.Text.Trim());
            if (defer)
            {
                if (links.Length > 1 && FileName.Text.Length > 0) throw new FormatException(Strings.Get("Errors.BatchFileName"));
                var requests = new JsonArray(links.Select(link => (JsonNode?)BuildRequest(link)).ToArray());
                if (links.Length == 1 && resolved is not null && DirectDownload.IsChecked != true)
                {
                    if (Files.Items.Count > 1 && Files.SelectedItems.Count == 0) throw new FormatException(Strings.Get("Errors.SelectFile"));
                    requests[0]!["opts"]!["selectFiles"] = new JsonArray(Files.SelectedItems.Cast<ResolvedFile>().Select(file => (JsonNode?)JsonValue.Create(file.Index)).ToArray());
                }
                await core.SendAsync(HttpMethod.Post, "native/queue", new JsonObject { ["reqs"] = requests, ["startAt"] = startAt?.ToString("O") });
                complete = true;
            }
            else if (links.Length > 1)
            {
                if (FileName.Text.Length > 0) throw new FormatException(Strings.Get("Errors.BatchFileName"));
                var requests = new JsonArray(links.Select(link => (JsonNode?)BuildRequest(link)).ToArray());
                await core.SendAsync(HttpMethod.Post, "tasks/batch", new JsonObject { ["reqs"] = requests }); complete = true;
            }
            else if (resolved is null && DirectDownload.IsChecked != true)
            {
                await InspectAsync(links[0]);
            }
            else
            {
                // Update resolution options if destination, file name or file selection changed after probing.
                var request = BuildRequest(links[0]);
                if (DirectDownload.IsChecked != true) { request["opts"]!["selectFiles"] = new JsonArray(Files.SelectedItems.Cast<ResolvedFile>().Select(file => JsonValue.Create(file.Index) as JsonNode).ToArray()); if (Files.Items.Count > 1 && Files.SelectedItems.Count == 0) throw new FormatException(Strings.Get("Errors.SelectFile")); }
                CreatedTaskId = (await core.SendAsync(HttpMethod.Post, "tasks", request))!.GetValue<string>(); complete = true;
            }
        }
        catch (Exception e) { ShowError(e); }
        finally { SetBusy(false); }
        if (complete) { var prefs = UiPreferences.Load(); if (prefs.RememberDownloadDirectory) prefs.LastDownloadDirectory = Destination.Text.Trim(); prefs.RecentLinks = linksForHistory().Concat(prefs.RecentLinks).Distinct().Take(30).ToList(); prefs.Save(); }
        return complete;
    }
    private async Task InspectAsync(string url)
    {
        Busy.Visibility = Visibility.Visible; Busy.IsActive = true;
        try
        {
            var result = (await core.SendAsync(HttpMethod.Post, "resolve", BuildRequest(url)))!;
            resolved = result["id"]!.GetValue<string>(); var resource = result["res"]!;
            var displayName = resource["name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(displayName)) displayName = resource["files"]!.AsArray()[0]!["name"]!.GetValue<string>();
            var size = resource["size"]!.GetValue<long>();
            Preview.Text = size > 0 ? DownloadItem.FormatBytes(size) : Strings.Get("Download.SizeFromSource");
            Files.Items.Clear(); var index = 0;
            foreach (var file in resource["files"]!.AsArray()) Files.Items.Add(new ResolvedFile(index++, Path.Combine(file!["path"]?.GetValue<string>() ?? "", file["name"]!.GetValue<string>()), file["size"]!.GetValue<long>()));
            Files.SelectAll(); Files.Visibility = Files.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed; FileSelectionActions.Visibility = FilesSurface.Visibility = Files.Visibility;
            if (initial?["opts"]?["selectFiles"] is JsonArray selected && selected.Count > 0) { var indexes = selected.Select(x => x!.GetValue<int>()).ToHashSet(); foreach (var file in Files.SelectedItems.Cast<ResolvedFile>().ToList()) if (!indexes.Contains(file.Index)) Files.SelectedItems.Remove(file); }
            if (initial is not null && Files.Items.Count == 1 && FileName.Text.Length == 0) FileName.Text = displayName;
            SetAction(Strings.Get("Download.Start"));
        }
        finally { Busy.IsActive = false; Busy.Visibility = Visibility.Collapsed; }
    }
}
