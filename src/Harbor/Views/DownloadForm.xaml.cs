using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.Models;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed partial class DownloadForm : UserControl
{
    private readonly CoreClient core;
    private string? resolved;
    private readonly JsonObject? initial;
    private readonly nint owner;
    private string lastInput = "";
    private readonly DownloadOptionsPanel requestOptions = new();
    public string? CreatedTaskId { get; private set; }
    public string ActionText { get; private set; } = Strings.Get("Download.CheckLink");
    public bool IsBusy { get; private set; }
    public event Action? StateChanged;
    public event Action? LayoutChanged;
    public double MeasureContentHeight(double width) { FormContent.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity)); return FormContent.DesiredSize.Height; }
    public DownloadForm(CoreClient core, JsonObject? initial = null, nint? owner = null, bool compact = false)
    {
        this.core = core; this.initial = initial; this.owner = owner ?? App.WindowHandle; InitializeComponent();
        OptionsContent.Children.Add(requestOptions);
        requestOptions.RequestChanged += InvalidateResolution;
        requestOptions.LayoutChanged += () => LayoutChanged?.Invoke();
        Headers.TextChanged += (_, _) => InvalidateResolution();
        FormContent.SizeChanged += (_, _) => LayoutChanged?.Invoke();
        if (compact) { Links.AcceptsReturn = false; Links.TextWrapping = TextWrapping.NoWrap; Links.MinHeight = 32; Links.MaxHeight = double.PositiveInfinity; ManualActions.Visibility = Visibility.Collapsed; }
        Loaded += InitializeForm;
    }
    public Button DetachOptionsButton()
    {
        ManualActions.Children.Remove(AdvancedButton);
        return AdvancedButton;
    }
    private void ToggleOptions(object sender, RoutedEventArgs e)
    {
        var expanded = OptionsSurface.Visibility != Visibility.Visible;
        OptionsSurface.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        OptionsArrow.Glyph = expanded ? "\uE70E" : "\uE70D";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(AdvancedButton, Strings.Get(expanded ? "Common.Collapse" : "Common.Expand"));
        LayoutChanged?.Invoke();
    }
    private void OptionsExpanding(Expander sender, ExpanderExpandingEventArgs args) => DispatcherQueue.TryEnqueue(() => LayoutChanged?.Invoke());
    private void OptionsCollapsed(Expander sender, ExpanderCollapsedEventArgs args) => DispatcherQueue.TryEnqueue(() => LayoutChanged?.Invoke());
    private void InputChanged(object s, TextChangedEventArgs e) { if (Files is null || lastInput == Links.Text) return; lastInput = Links.Text; InvalidateResolution(); }
    private void InvalidateResolution()
    {
        resolved = null;
        var links = ConfigJson.Lines(Links.Text);
        SetAction(links.Length > 1 ? Strings.Format("Download.StartBatch", links.Length) : DirectDownload.IsChecked == true ? Strings.Get("Download.Start") : Strings.Get("Download.CheckLink"));
        KindLabel.Text = links.Length > 1 ? Strings.Get("Download.Batch") : links.FirstOrDefault() is { } link && DownloadSources.IsTorrent(link) ? "BT" : Strings.Get("Common.File");
        Files.Visibility = Visibility.Collapsed;
        FileSelectionActions.Visibility = Visibility.Collapsed;
        FilesSurface.Visibility = Visibility.Collapsed;
        Preview.Text = DirectDownload.IsChecked == true ? Strings.Get("Download.SizeAfterStart") : Strings.Get("Download.SizeAfterCheck");
    }
    private void DirectChanged(object sender, RoutedEventArgs e) { if (Files is not null) InvalidateResolution(); }
    private void CategoryChanged(object sender, SelectionChangedEventArgs e) { if (Category.SelectedItem is ComboBoxItem item) Destination.Text = (string)item.Tag; }
    private void ShowError(Exception e) { Message.Message = UserError.Message(e); Message.Visibility = Visibility.Visible; Message.IsOpen = true; LayoutChanged?.Invoke(); }
    private void SetAction(string text) { ActionText = text; StateChanged?.Invoke(); LayoutChanged?.Invoke(); }
    private void SetBusy(bool value) { IsBusy = value; Busy.IsActive = value; Busy.Visibility = value ? Visibility.Visible : Visibility.Collapsed; StateChanged?.Invoke(); }
    private sealed record ResolvedFile(int Index, string Name, long Size) { public override string ToString() => $"{Name} · {DownloadItem.FormatBytes(Size)}"; }
}
