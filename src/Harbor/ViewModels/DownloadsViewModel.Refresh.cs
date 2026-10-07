using Harbor.Models;
using Harbor.Services;

namespace Harbor.ViewModels;

public sealed partial class DownloadsViewModel
{
    private Task? refreshTask;

    public async Task RefreshAsync()
    {
        if (refreshTask is not null) { await refreshTask; return; }
        refreshTask = RefreshCoreAsync();
        try { await refreshTask; }
        finally { refreshTask = null; }
    }

    private async Task RefreshCoreAsync()
    {
        try
        {
            // These independent upstream and Harbor reads share one refresh cycle.
            var tasks = Core.GetAsync("tasks", lifetime.Token);
            var queue = Core.GetAsync("native/queue", lifetime.Token);
            await Task.WhenAll(tasks, queue);
            var ids = new HashSet<string>();
            var changes = DownloadChanges.None;
            foreach (var node in tasks.Result!.AsArray().Concat(queue.Result!.AsArray()))
            {
                var entry = node!.AsObject(); var id = entry["id"]!.GetValue<string>(); ids.Add(id);
                if (items.TryGetValue(id, out var item)) changes |= item.Update(entry);
                else { items[id] = new(entry); changes |= DownloadChanges.Content; }
            }
            foreach (var id in items.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                items.Remove(id); changes |= DownloadChanges.Content;
            }
            if ((changes & DownloadChanges.Content) != 0 || Sort == "progress" && changes != DownloadChanges.None) ApplyFilter();
            else if (changes != DownloadChanges.None) UpdateSummary();
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception e) { Error = UserError.Message(e); }
    }
}
