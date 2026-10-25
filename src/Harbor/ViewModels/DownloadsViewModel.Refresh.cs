using Harbor.Models;
using Harbor.Services;
using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Harbor.ViewModels;

public sealed partial class DownloadsViewModel
{
    // Finished tasks dominate the history and a torrent's metadata lists every
    // file, so live ticks fetch only unfinished work between full snapshots.
    private static readonly TimeSpan fullRefreshInterval = TimeSpan.FromSeconds(10);
    private Task? refreshTask;
    private bool refreshingFull;
    private long lastFullRefresh;

    public async Task RefreshAsync(bool full = true)
    {
        while (refreshTask is { IsCompleted: false } pending)
        {
            var covered = refreshingFull || !full;
            await pending;
            if (covered)
                return;
        }
        refreshingFull = full || lastFullRefresh == 0 || Stopwatch.GetElapsedTime(lastFullRefresh) >= fullRefreshInterval;
        var task = RefreshCoreAsync(refreshingFull);
        refreshTask = task;
        try
        {
            await task;
        }
        finally
        {
            if (refreshTask == task)
                refreshTask = null;
        }
    }

    private async Task RefreshCoreAsync(bool full)
    {
        try
        {
            // These independent upstream and Harbor reads share one refresh cycle.
            var queue = Core.GetAsync("native/queue", lifetime.Token);
            HashSet<string> watched = full ? [] : items.Values.Where(i => i.IsComplete && (i.Uploading || i.IsProcessing)).Select(i => i.Id).ToHashSet();
            var tasks = Core.GetAsync(full ? "tasks" : "tasks?notStatus=done", lifetime.Token);
            var finished = watched.Count > 0 ? Core.GetAsync("tasks?" + string.Join("&", watched.Select(id => "id=" + Uri.EscapeDataString(id))), lifetime.Token) : Task.FromResult<JsonNode?>(new JsonArray());
            await Task.WhenAll(queue, tasks, finished);
            var entries = new Dictionary<string, JsonObject>();
            foreach (var node in tasks.Result!.AsArray().Concat(finished.Result!.AsArray()).Concat(queue.Result!.AsArray()))
                entries.TryAdd(node!["id"]!.GetValue<string>(), node.AsObject());
            // A partial snapshot omits finished tasks, so it only proves that deferred entries left.
            // An unfinished or watched task missing from it finished or was removed: take a full snapshot.
            var missing = items.Values.Where(i => !entries.ContainsKey(i.Id) && (full || i.IsDeferred || !i.IsComplete || watched.Contains(i.Id))).ToArray();
            if (!full && missing.Any(i => !i.IsDeferred))
            {
                await RefreshCoreAsync(true);
                return;
            }
            var changes = DownloadChanges.None;
            foreach (var (id, entry) in entries)
            {
                if (items.TryGetValue(id, out var item))
                    changes |= item.Update(entry);
                else
                {
                    items[id] = new(entry);
                    changes |= DownloadChanges.Content;
                }
            }
            foreach (var item in missing)
            {
                items.Remove(item.Id);
                changes |= DownloadChanges.Content;
            }
            if (full)
                lastFullRefresh = Stopwatch.GetTimestamp();
            if ((changes & DownloadChanges.Content) != 0 || Sort == "progress" && changes != DownloadChanges.None)
                ApplyFilter();
            else if (changes != DownloadChanges.None)
                UpdateSummary();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { Error = UserError.Message(e); }
    }
}
