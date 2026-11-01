using Harbor.Localization;
using Harbor.Glass;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed class DownloadWindow
{
    private readonly Grid surface = new();
    private readonly Grid pageHost = new();
    private readonly WindowHeader header = new();
    private readonly CoreClient core = new();
    private readonly JsonObject request;
    private readonly bool compact;
    private readonly ConfirmationTopmost confirmationTopmost;
    private bool closed;
    private DownloadProgressPage? progress;
    public string? TaskId
    {
        get; private set;
    }
    private Func<double, double>? preferredHeight;
    private const double ContentWidth = 660;
    private const double MaximumContentHeight = 640;
    private bool fitQueued;
    private Windows.Graphics.RectInt32 lastBounds;
    internal GlassWindow Glass { get; }
    public event EventHandler? Closed;
    private string Title
    {
        set { Glass.Title = value; header.Title = value; }
    }

    public DownloadWindow(JsonObject request, bool compact = true)
    {
        this.request = request;
        this.compact = compact;
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(pageHost, 1);
        surface.Children.Add(header);
        surface.Children.Add(pageHost);
        var root = new UserControl { Content = surface, RequestedTheme = WindowAppearance.Theme, IsTabStop = false };
        Glass = new GlassWindow(root, Strings.Get("Downloads.Add"), ContentWidth, 360, resizable: false, minWidth: 480, minHeight: 140) { IsMinimizable = false };
        Title = Strings.Get("Downloads.Add");
        header.ShowMinimize = false;
        header.MinimizeRequested += Glass.Minimize;
        header.CloseRequested += Close;
        Glass.SetCaption(header);
        confirmationTopmost = new ConfirmationTopmost(Glass);
        Glass.Closed += (_, _) => { closed = true; confirmationTopmost.Dispose(); progress?.Stop(); core.Dispose(); Closed?.Invoke(this, EventArgs.Empty); };
        Glass.Activated += (_, _) => progress?.UpdatePollingVisibility();
        Glass.VisibilityChanged += (_, _) => progress?.UpdatePollingVisibility();
        surface.Loaded += Confirm;
    }

    public DownloadWindow(string taskId) : this(new JsonObject(), compact: false)
    {
        TaskId = taskId;
        confirmationTopmost.Dispose();
        Title = Strings.Get("Downloads.Progress");
        ShowMinimize();
    }

    private void ShowMinimize()
    {
        Glass.IsMinimizable = true;
        header.ShowMinimize = true;
    }

    public void Close() => Glass.Close();

    private async void Confirm(object sender, RoutedEventArgs e)
    {
        surface.Loaded -= Confirm;
        try
        {
            await core.ConnectAsync();
        }
        catch (Exception error)
        {
            if (!closed)
            {
                await NativeDialogs.ShowAsync(new ContentDialog { Title = Strings.Get("Errors.LoadDownloads"), Content = UserError.Message(error), CloseButtonText = Strings.Get("Common.Close") }, surface.XamlRoot);
                Close();
            }
            return;
        }
        if (closed)
            return;
        if (TaskId is { } taskId)
        {
            ShowProgress(taskId);
            return;
        }
        if (await TryUpdateSource())
            return;
        var page = new DownloadConfirmationPage(core, request, Glass.Handle, compact);
        preferredHeight = page.PreferredHeight;
        page.LayoutChanged += RequestFit;
        page.Started += ShowProgress;
        page.Cancelled += Close;
        page.Deferred += () => { ((App)Application.Current).ShowDeferredDownloads(); Close(); };
        pageHost.Children.Add(page);
        RequestFit();
    }

    private async Task<bool> TryUpdateSource()
    {
        var pending = UiPreferences.Load().PendingUpdateTaskId;
        if (pending.Length == 0)
            return false;
        try
        {
            var item = new Harbor.Models.DownloadItem((await core.GetAsync("tasks/" + Uri.EscapeDataString(pending)))!.AsObject());
            var error = new InfoBar { Severity = InfoBarSeverity.Error };
            NativeInfoBars.CollapseWhenClosed(error);
            var panel = new StackPanel { Spacing = 12, Children = { error, new TextBlock { Text = item.Name, TextWrapping = TextWrapping.Wrap }, new TextBlock { Text = request["req"]?["url"]?.GetValue<string>() ?? "", TextWrapping = TextWrapping.Wrap } } };
            var dialog = new ContentDialog { Title = Strings.Get("Source.ResumeConfirm"), Content = panel, PrimaryButtonText = Strings.Get("Source.UpdateResume"), IsPrimaryButtonEnabled = item.CanEditSource, SecondaryButtonText = Strings.Get("Source.CreateNew"), CloseButtonText = Strings.Get("Common.Cancel") };
            dialog.PrimaryButtonClick += async (_, click) => { var deferral = click.GetDeferral(); try { await core.SendAsync(System.Net.Http.HttpMethod.Patch, "tasks/" + pending, new JsonObject { ["req"] = request["req"]!.DeepClone() }); await core.SendAsync(System.Net.Http.HttpMethod.Put, "tasks/" + pending + "/continue"); } catch (Exception failure) { click.Cancel = true; error.Message = UserError.Message(failure); error.IsOpen = true; } finally { deferral.Complete(); } };
            var result = await NativeDialogs.ShowAsync(dialog, surface.XamlRoot);
            if (result == ContentDialogResult.None)
            {
                Close();
                return true;
            }
            var prefs = UiPreferences.Load();
            prefs.PendingUpdateTaskId = "";
            prefs.Save();
            if (result == ContentDialogResult.Primary)
            {
                ShowProgress(pending);
                return true;
            }
            return false;
        }
        catch (Exception error)
        {
            var dialog = new ContentDialog { Title = Strings.Get("Source.UpdateFailed"), Content = UserError.Message(error), PrimaryButtonText = Strings.Get("Source.CreateNew"), CloseButtonText = Strings.Get("Common.Cancel") };
            if (await NativeDialogs.ShowAsync(dialog, surface.XamlRoot) != ContentDialogResult.Primary)
            {
                Close();
                return true;
            }
            var prefs = UiPreferences.Load();
            prefs.PendingUpdateTaskId = "";
            prefs.Save();
            return false;
        }
    }

    private void ShowProgress(string id)
    {
        confirmationTopmost.Dispose();
        TaskId = id;
        Title = Strings.Get("Downloads.Progress");
        pageHost.Children.Clear();
        progress = new DownloadProgressPage(core, id, Close, () => Glass.IsVisible);
        preferredHeight = progress.PreferredHeight;
        progress.LayoutChanged += RequestFit;
        progress.TitleChanged += title => Title = title;
        pageHost.Children.Add(progress);
        WindowActivation.SetTopmost(Glass, false);
        ShowMinimize();
        RequestFit();
    }

    private void RequestFit()
    {
        if (closed || fitQueued)
            return;
        fitQueued = true;
        surface.DispatcherQueue.TryEnqueue(() =>
        {
            fitQueued = false;
            if (closed || preferredHeight is null || surface.XamlRoot is null)
                return;
            var scale = surface.XamlRoot.RasterizationScale;
            var area = Glass.WorkArea;
            var width = Math.Min(ContentWidth, area.Width / scale - 24);
            header.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
            var height = Math.Clamp(preferredHeight(width) + header.DesiredSize.Height, 140, Math.Min(MaximumContentHeight, area.Height / scale - 48));
            int w = (int)Math.Round(width * scale), h = (int)Math.Ceiling(height * scale);
            var current = Glass.Bounds;
            if (w == lastBounds.Width && h == lastBounds.Height && current.X == lastBounds.X && current.Y == lastBounds.Y)
                return;
            // The first fit centers the window; later ones keep the top edge and expand or collapse downward,
            // pushed up only as far as the work area needs, and glide there.
            var first = lastBounds.Width == 0;
            var x = Math.Clamp((int)Math.Round(current.X + current.Width / 2.0 - w / 2.0), area.X, area.X + area.Width - w);
            var y = Math.Clamp(first ? (int)Math.Round(current.Y + current.Height / 2.0 - h / 2.0) : current.Y, area.Y, area.Y + area.Height - h);
            lastBounds = new Windows.Graphics.RectInt32(x, y, w, h);
            if (first) Glass.Bounds = lastBounds; else Glass.AnimateBounds(lastBounds);
            confirmationTopmost.Refresh();
        });
    }
}
