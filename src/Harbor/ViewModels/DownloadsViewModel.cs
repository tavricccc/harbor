using Harbor.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using Harbor.Models;
using Harbor.Services;
using System.Collections.ObjectModel;

namespace Harbor.ViewModels;

public sealed partial class DownloadsViewModel : ObservableObject, IDisposable
{
    public CoreClient Core { get; } = new();
    private readonly Dictionary<string, DownloadItem> items = [];
    private readonly CancellationTokenSource lifetime = new();
    public ObservableCollection<DownloadItem> VisibleItems { get; } = [];
    public IEnumerable<DownloadItem> AllItems => items.Values;
    [ObservableProperty]
    public partial DownloadItem? Selected
    {
        get; set;
    }
    [ObservableProperty] public partial string Summary { get; set; } = Strings.Get("Downloads.Loading");
    [ObservableProperty] public partial string Error { get; set; } = "";
    [ObservableProperty]
    public partial bool IsConnected
    {
        get; set;
    }
    public string Filter { get; set; } = "all";
    public string Search { get; set; } = "";
    public string Sort { get; set; } = "newest";
    public IReadOnlyList<DownloadItem> Selection { get; private set; } = [];
    public bool CanPauseSelected => Selection.Any(x => x.CanPause);
    public bool CanResumeSelected => Selection.Any(x => x.CanResume);
    public bool CanPauseAll => IsConnected && items.Values.Any(x => x.CanPause);
    public bool CanResumeAll => IsConnected && items.Values.Any(x => x.CanResume && (!x.IsDeferred || x.ScheduledAt is null));
    public bool CanClearCompleted => items.Values.Any(x => x.IsComplete && !x.Uploading && !x.IsProcessing);
    public bool HasSelection => Selection.Count > 0;
    public bool HasSingleSelection => Selected is not null;
    public bool HasActivity => items.Values.Any(i => i.CanPause || i.IsProcessing);
    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(HasActivity ? 1 : 5);
    public bool CanOpenSelected => Selected?.IsComplete == true;
    public string PrimaryActionKey => Selected is not null ? Selected.PrimaryAction.Key : Selection.Count == 0 ? "none" : CanPauseSelected ? "pause" : CanResumeSelected ? "continue" : "folder";
    public string PrimaryActionLabel => Selected?.PrimaryActionLabel ?? PrimaryActionKey switch { "pause" => Strings.Get("Downloads.PauseSelected"), "continue" => Strings.Get("Downloads.ResumeSelected"), "folder" => Strings.Get("Downloads.OpenSaveFolder"), _ => Strings.Get("Downloads.Select") };
    public string PrimaryActionGlyph => Selected?.PrimaryActionGlyph ?? PrimaryActionKey switch { "pause" => "\uE769", "continue" => "\uE768", "folder" => "\uE8B7", _ => "\uE896" };
    public bool CanActSelected => PrimaryActionKey != "none";
    public bool CanEditSource => Selected?.CanEditSource == true;
    public bool CanShowProgress => Selected?.IsDeferred == false;
    public string EmptyTitle => items.Count == 0 ? Strings.Get("Downloads.EmptyTitle") : Strings.Get("Downloads.NoMatches");
    public string EmptyHint => items.Count == 0 ? Strings.Get("Downloads.EmptyHint") : Strings.Get("Downloads.NoMatchesHint");
    partial void OnSelectedChanged(DownloadItem? oldValue, DownloadItem? newValue)
    {
        if (oldValue is not null)
            oldValue.PropertyChanged -= SelectionUpdated;
        if (newValue is not null)
            newValue.PropertyChanged += SelectionUpdated;
        SelectionUpdated(this, new System.ComponentModel.PropertyChangedEventArgs(null));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(CanEditSource));
    }
    public void SetSelection(IEnumerable<DownloadItem> values)
    {
        Selection = values.ToList();
        Selected = Selection.Count == 1 ? Selection[0] : null;
        SelectionUpdated(this, new(null));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        UpdateSummary();
    }
    private void SelectionUpdated(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
            return;
        OnPropertyChanged(nameof(CanPauseSelected));
        OnPropertyChanged(nameof(CanResumeSelected));
        OnPropertyChanged(nameof(CanOpenSelected));
        OnPropertyChanged(nameof(CanEditSource));
        OnPropertyChanged(nameof(CanShowProgress));
        OnPropertyChanged(nameof(PrimaryActionLabel));
        OnPropertyChanged(nameof(PrimaryActionGlyph));
        OnPropertyChanged(nameof(CanActSelected));
    }

    public async Task InitializeAsync()
    {
        try
        {
            await Core.ConnectAsync();
            IsConnected = true;
            await RefreshAsync();
            if (Error.Length == 0)
                UpdateSummary();
        }
        catch (Exception e) { Error = UserError.Message(e); Summary = Strings.Get("Errors.LoadDownloads"); }
    }
    private void UpdateSummary()
    {
        var active = items.Values.Count(i => i.Status == "running");
        var speed = DownloadItem.FormatBytes(items.Values.Where(i => i.Status == "running").Sum(i => i.Speed));
        Summary = (VisibleItems.Count == items.Count ? Strings.Format("Downloads.Count", items.Count) : Strings.Format("Downloads.FilteredCount", VisibleItems.Count, items.Count))
            + (active > 0 ? Strings.Format("Downloads.ActiveSummary", active, speed) : "")
            + (Selection.Count > 0 ? Strings.Format("Downloads.SelectionSummary", Selection.Count) : "");
    }
    public void Dispose()
    {
        lifetime.Cancel();
        if (Selected is not null)
            Selected.PropertyChanged -= SelectionUpdated;
        Core.Dispose();
        lifetime.Dispose();
    }
}
