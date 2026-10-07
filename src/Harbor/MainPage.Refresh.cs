using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace Harbor;

public sealed partial class MainPage
{
    private readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private bool polling;

    private void InitializeRefresh()
    {
        searchTimer.Tick += (_, _) => { searchTimer.Stop(); ViewModel.ApplyFilter(); };
        timer.Tick += async (_, _) =>
        {
            if (!WindowVisible()) { timer.Stop(); polling = false; return; }
            if (ViewModel.IsConnected) await ViewModel.RefreshAsync();
            timer.Interval = ViewModel.RefreshInterval;
        };
    }

    private static bool WindowVisible() => App.Window?.AppWindow is { IsVisible: true } window
        && window.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized };

    public async void UpdatePollingVisibility()
    {
        if (!ready.Task.IsCompleted) return;
        var visible = WindowVisible() && DownloadsSurface.Visibility == Visibility.Visible;
        if (!visible) { timer.Stop(); polling = false; return; }
        if (polling) return;
        polling = true;
        if (ViewModel.IsConnected) await ViewModel.RefreshAsync();
        if (!polling) return;
        timer.Interval = ViewModel.RefreshInterval;
        timer.Start();
    }
}
