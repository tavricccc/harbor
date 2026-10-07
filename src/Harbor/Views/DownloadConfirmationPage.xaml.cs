using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class DownloadConfirmationPage : Page
{
    private readonly DownloadForm form;
    public event Action<string>? Started;
    public event Action? Cancelled;
    public event Action? Deferred;
    public event Action? LayoutChanged;
    public double PreferredHeight(double width)
    {
        Footer.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity));
        return form.MeasureContentHeight(width - 48) + 36 + Footer.DesiredSize.Height;
    }
    public DownloadConfirmationPage(CoreClient core, JsonObject request, nint owner, bool compact = true)
    {
        InitializeComponent();
        form = new DownloadForm(core, request, owner, compact);
        FormHost.Content = form;
        Actions.Children.Insert(0, form.DetachOptionsButton());
        ScheduleDate.Date = DateTimeOffset.Now.AddHours(1);
        ScheduleDate.MinDate = DateTimeOffset.Now.Date;
        ScheduleTime.Time = DateTime.Now.AddHours(1).TimeOfDay;
        form.StateChanged += () => { StartButton.Content = form.ActionText; StartButton.IsEnabled = LaterButton.IsEnabled = ScheduleButton.IsEnabled = !form.IsBusy; };
        form.LayoutChanged += () => LayoutChanged?.Invoke();
    }
    private async void Start(object sender, RoutedEventArgs e)
    {
        if (!await form.SubmitAsync()) return;
        if (form.CreatedTaskId is { } id) Started?.Invoke(id);
        else Cancelled?.Invoke();
    }
    private void Cancel(object sender, RoutedEventArgs e) => Cancelled?.Invoke();
    private async void DownloadLater(object sender, RoutedEventArgs e)
    {
        if (await form.SubmitAsync(defer: true)) Deferred?.Invoke();
    }
    private async void ScheduleDownload(object sender, RoutedEventArgs e)
    {
        if (ScheduleDate.Date is not { } date) return;
        var local = DateTime.SpecifyKind(date.Date + ScheduleTime.Time, DateTimeKind.Local);
        ScheduleButton.Flyout.Hide();
        if (await form.SubmitAsync(defer: true, startAt: new DateTimeOffset(local))) Deferred?.Invoke();
    }
    private void CancelShortcut(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args) { Cancelled?.Invoke(); args.Handled = true; }
    private void StartShortcut(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args) { if (StartButton.IsEnabled) Start(sender,new()); args.Handled = true; }
}
