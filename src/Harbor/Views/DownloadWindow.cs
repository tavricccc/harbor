using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed class DownloadWindow : Window
{
    private readonly Grid surface = new();
    private readonly Grid pageHost = new();
    private readonly CoreClient core = new();
    private readonly JsonObject request;
    private readonly bool compact;
    private bool closed;
    private DownloadProgressPage? progress;
    public string? TaskId
    {
        get; private set;
    }
    private Func<double, double>? preferredHeight;
    private const double ContentWidth = 660;
    private const double MaximumContentHeight = 600;
    private bool fitQueued;
    private Windows.Graphics.SizeInt32 lastClientSize;

    public DownloadWindow(JsonObject request, bool compact = true)
    {
        this.request = request;
        this.compact = compact;
        Title = Strings.Get("Downloads.Add");
        surface.RequestedTheme = WindowAppearance.Theme;
        surface.Style = (Style)Application.Current.Resources["DownloadSurfaceStyle"];
        surface.Children.Add(pageHost);
        Content = surface;
        WindowAppearance.ApplyFrame(this, surface);
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(Math.Min((int)(ContentWidth * scale), area.Width), Math.Min((int)(360 * scale), area.Height)));
        AppWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - AppWindow.Size.Width) / 2, area.Y + (area.Height - AppWindow.Size.Height) / 2));
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(600 * scale);
            presenter.PreferredMinimumHeight = (int)(140 * scale);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        Closed += (_, _) => { closed = true; progress?.Stop(); core.Dispose(); };
        Activated += (_, _) => progress?.UpdatePollingVisibility();
        AppWindow.Changed += (_, args) => { if (args.DidVisibilityChange || args.DidPresenterChange) progress?.UpdatePollingVisibility(); };
        surface.Loaded += Confirm;
    }

    public DownloadWindow(string taskId) : this(new JsonObject(), compact: false)
    {
        TaskId = taskId;
        Title = Strings.Get("Downloads.Progress");
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = false;
            presenter.IsMinimizable = true;
        }
    }

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
        var page = new DownloadConfirmationPage(core, request, WinRT.Interop.WindowNative.GetWindowHandle(this), compact);
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
        TaskId = id;
        Title = Strings.Get("Downloads.Progress");
        pageHost.Children.Clear();
        progress = new DownloadProgressPage(core, id, Close, () => AppWindow.IsVisible
            && AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Minimized });
        preferredHeight = progress.PreferredHeight;
        progress.LayoutChanged += RequestFit;
        progress.TitleChanged += title => Title = title;
        pageHost.Children.Add(progress);
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = false;
            presenter.IsMinimizable = true;
        }
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
            if (closed || preferredHeight is null)
                return;
            var scale = surface.XamlRoot.RasterizationScale;
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
            var width = Math.Min(ContentWidth, area.Width / scale - 24);
            var height = Math.Clamp(preferredHeight(width), 140, Math.Min(MaximumContentHeight, area.Height / scale - 48));
            var size = new Windows.Graphics.SizeInt32((int)Math.Round(width * scale), (int)Math.Ceiling(height * scale));
            if (size.Width == lastClientSize.Width && size.Height == lastClientSize.Height)
                return;
            var position = AppWindow.Position;
            var previousSize = AppWindow.Size;
            var centerX = position.X + previousSize.Width / 2.0;
            var centerY = position.Y + previousSize.Height / 2.0;
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                presenter.IsResizable = true;
            AppWindow.ResizeClient(size);
            lastClientSize = size;
            position.X = (int)Math.Round(centerX - AppWindow.Size.Width / 2.0);
            position.Y = (int)Math.Round(centerY - AppWindow.Size.Height / 2.0);
            position.X = Math.Clamp(position.X, area.X, area.X + area.Width - AppWindow.Size.Width);
            position.Y = Math.Clamp(position.Y, area.Y, area.Y + area.Height - AppWindow.Size.Height);
            AppWindow.Move(position);
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter fixedPresenter)
                fixedPresenter.IsResizable = false;
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
