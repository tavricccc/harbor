using CommunityToolkit.Mvvm.ComponentModel;
using Gopeed_Native.Models;
using Gopeed_Native.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Gopeed_Native.ViewModels;

public sealed partial class DownloadsViewModel : ObservableObject, IDisposable
{
    public CoreClient Core { get; } = new();
    private readonly Dictionary<string, DownloadItem> items = [];
    public ObservableCollection<DownloadItem> VisibleItems { get; } = [];
    public IEnumerable<DownloadItem> AllItems => items.Values;
    [ObservableProperty] public partial DownloadItem? Selected { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = "正在載入下載…";
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty] public partial bool IsConnected { get; set; }
    public string Filter { get; set; } = "all";
    public string Search { get; set; } = "";
    public string Sort { get; set; } = "newest";
    public IReadOnlyList<DownloadItem> Selection { get; private set; } = [];
    public bool CanPauseSelected => Selection.Any(x => x.CanPause);
    public bool CanResumeSelected => Selection.Any(x => x.CanResume);
    public bool CanPauseAll => IsConnected && items.Values.Any(x => x.CanPause);
    public bool CanResumeAll => IsConnected && items.Values.Any(x => x.CanResume);
    public bool CanClearCompleted => items.Values.Any(x => x.IsComplete && !x.Uploading && !x.IsProcessing);
    public bool HasSelection => Selection.Count > 0;
    public bool HasSingleSelection => Selected is not null;
    public bool CanOpenSelected => Selected?.IsComplete == true;
    public string PrimaryActionKey => Selected is not null ? Selected.PrimaryAction.Key : Selection.Count == 0 ? "none" : CanPauseSelected ? "pause" : CanResumeSelected ? "continue" : "folder";
    public string PrimaryActionLabel => Selected?.PrimaryActionLabel ?? PrimaryActionKey switch { "pause" => "暫停選取的下載", "continue" => "繼續選取的下載", "folder" => "開啟儲存資料夾", _ => "選取下載" };
    public string PrimaryActionGlyph => Selected?.PrimaryActionGlyph ?? PrimaryActionKey switch { "pause" => "\uE769", "continue" => "\uE768", "folder" => "\uE8B7", _ => "\uE896" };
    public bool CanActSelected => PrimaryActionKey != "none";
    public bool CanEditSource => Selected?.CanEditSource == true;
    public string EmptyTitle => items.Count == 0 ? "還沒有下載" : "沒有符合條件的下載";
    public string EmptyHint => items.Count == 0 ? "貼上網址開始下載，也可以一次加入多個連結。" : "試著清除搜尋，或切換為全部下載。";
    partial void OnSelectedChanged(DownloadItem? oldValue, DownloadItem? newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= SelectionUpdated;
        if (newValue is not null) newValue.PropertyChanged += SelectionUpdated;
        SelectionUpdated(this, new System.ComponentModel.PropertyChangedEventArgs(null));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection)); OnPropertyChanged(nameof(CanEditSource));
    }
    public void SetSelection(IEnumerable<DownloadItem> values) { Selection = values.ToList(); Selected = Selection.Count == 1 ? Selection[0] : null; SelectionUpdated(this, new(null)); OnPropertyChanged(nameof(HasSelection)); UpdateSummary(); }
    private void SelectionUpdated(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanPauseSelected)); OnPropertyChanged(nameof(CanResumeSelected));
        OnPropertyChanged(nameof(CanOpenSelected));
        OnPropertyChanged(nameof(CanEditSource));
        OnPropertyChanged(nameof(PrimaryActionLabel)); OnPropertyChanged(nameof(PrimaryActionGlyph)); OnPropertyChanged(nameof(CanActSelected));
    }

    public async Task InitializeAsync()
    {
        try { await Core.ConnectAsync(); IsConnected = true; await RefreshAsync(); }
        catch (Exception e) { Error = UserError.Message(e); Summary = "無法載入下載"; }
    }
    public async Task RefreshAsync()
    {
        try
        {
            var data = (await Core.GetAsync("tasks"))!.AsArray();
            var ids = new HashSet<string>();
            foreach (var node in data)
            {
                var entry = node!.AsObject(); var id = entry["id"]!.GetValue<string>(); ids.Add(id);
                if (items.TryGetValue(id, out var item)) item.Update(entry); else items[id] = new(entry);
            }
            foreach (var id in items.Keys.Where(id => !ids.Contains(id)).ToList()) items.Remove(id);
            ApplyFilter();
        }
        catch (Exception e) { Error = UserError.Message(e); }
    }
    public void ApplyFilter()
    {
        var filtered = items.Values.Where(i => Filter switch { "active" => i.Status is "running" or "wait" or "ready", "done" => i.Status == "done", "pause" => i.Status == "pause", "error" => i.Status == "error", _ => true })
            .Where(i => i.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || i.Url.Contains(Search, StringComparison.OrdinalIgnoreCase));
        var ordered = Sort switch { "oldest" => filtered.OrderBy(x => x.CreatedAt), "name" => filtered.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase), "size" => filtered.OrderByDescending(x => x.Size), "progress" => filtered.OrderBy(x => x.Percent), _ => filtered.OrderByDescending(x => x.CreatedAt) };
        var visible = ordered.ToList();
        for (var index = VisibleItems.Count - 1; index >= 0; index--) if (!visible.Contains(VisibleItems[index])) VisibleItems.RemoveAt(index);
        for (var index = 0; index < visible.Count; index++)
        {
            var current = VisibleItems.IndexOf(visible[index]);
            if (current < 0) VisibleItems.Insert(index, visible[index]); else if (current != index) VisibleItems.Move(current, index);
        }
        if (Selected is not null && !VisibleItems.Contains(Selected)) Selected = null;
        SelectionUpdated(this, new(null));
        OnPropertyChanged(nameof(CanPauseAll)); OnPropertyChanged(nameof(CanResumeAll)); OnPropertyChanged(nameof(CanClearCompleted));
        UpdateSummary();
        OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyHint));
    }
    private void UpdateSummary()
    {
        var active = items.Values.Count(i => i.Status == "running");
        var speed = DownloadItem.FormatBytes(items.Values.Where(i => i.Status == "running").Sum(i => i.Speed));
        Summary = $"{VisibleItems.Count} / {items.Count} 個下載 · {active} 個進行中 · {speed}/s · 已選取 {Selection.Count} 個";
    }
    public async Task ActAsync(string action, IEnumerable<DownloadItem> targets, bool deleteFiles = false)
    {
        try
        {
            var list = targets.ToList(); if (list.Count == 0) return;
            var query = string.Join("&", list.Select(x => "id=" + Uri.EscapeDataString(x.Id)));
            await Core.SendAsync(action == "delete" ? HttpMethod.Delete : HttpMethod.Put, action == "delete" ? $"tasks?{query}&force={deleteFiles.ToString().ToLowerInvariant()}" : $"tasks/{action}?{query}");
            await RefreshAsync();
        }
        catch (Exception e) { Error = UserError.Message(e); }
    }
    public void Dispose() => Core.Dispose();
}
