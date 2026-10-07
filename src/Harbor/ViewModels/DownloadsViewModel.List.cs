namespace Harbor.ViewModels;

public sealed partial class DownloadsViewModel
{
    public void ApplyFilter()
    {
        var filtered = items.Values.Where(i => Filter switch
        {
            "active" => i.CanPause || i.IsProcessing,
            "deferred" => i.IsDeferred,
            "done" => i.IsComplete && !i.Uploading && !i.IsProcessing,
            "pause" => i.Status == "pause",
            "error" => i.Status == "error" || i.ExtractionStatus == "error" || i.IsDeferred && i.DeferredError.Length > 0,
            _ => true
        }).Where(i => i.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || i.Url.Contains(Search, StringComparison.OrdinalIgnoreCase));
        var ordered = Sort switch
        {
            "oldest" => filtered.OrderBy(x => x.CreatedAt),
            "name" => filtered.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase),
            "size" => filtered.OrderByDescending(x => x.Size),
            "progress" => filtered.OrderBy(x => x.Percent),
            _ => filtered.OrderByDescending(x => x.CreatedAt)
        };
        var visible = ordered.ToList();
        var keep = visible.ToHashSet();
        for (var index = VisibleItems.Count - 1; index >= 0; index--)
            if (!keep.Contains(VisibleItems[index])) VisibleItems.RemoveAt(index);
        for (var index = 0; index < visible.Count; index++)
        {
            if (index < VisibleItems.Count && ReferenceEquals(VisibleItems[index], visible[index])) continue;
            var current = VisibleItems.IndexOf(visible[index]);
            if (current < 0) VisibleItems.Insert(index, visible[index]); else VisibleItems.Move(current, index);
        }
        SetSelection(Selection.Where(keep.Contains));
        OnPropertyChanged(nameof(CanPauseAll)); OnPropertyChanged(nameof(CanResumeAll)); OnPropertyChanged(nameof(CanClearCompleted));
        OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyHint));
    }
}
