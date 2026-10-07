using Harbor.Models;
using Harbor.Services;
using System.Net.Http;

namespace Harbor.ViewModels;

public sealed partial class DownloadsViewModel
{
    public async Task ActAsync(string action, IEnumerable<DownloadItem> targets, bool deleteFiles = false)
    {
        try
        {
            var list = targets.ToList(); if (list.Count == 0) return;
            foreach (var item in list.Where(x => x.IsDeferred))
                await Core.SendAsync(action == "delete" ? HttpMethod.Delete : HttpMethod.Put, $"native/queue/{Uri.EscapeDataString(item.Id)}" + (action == "delete" ? "" : "/start"));
            var active = list.Where(x => !x.IsDeferred).ToList();
            if (active.Count > 0)
            {
                var query = string.Join("&", active.Select(x => "id=" + Uri.EscapeDataString(x.Id)));
                await Core.SendAsync(action == "delete" ? HttpMethod.Delete : HttpMethod.Put, action == "delete" ? $"tasks?{query}&force={deleteFiles.ToString().ToLowerInvariant()}" : $"tasks/{action}?{query}");
            }
            // A snapshot already in flight may predate the action.
            if (refreshTask is not null) await refreshTask;
            await RefreshAsync();
        }
        catch (Exception e) { Error = UserError.Message(e); }
    }
}
