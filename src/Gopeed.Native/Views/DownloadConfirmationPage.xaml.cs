using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using System.Text.Json.Nodes;

namespace Gopeed_Native.Views;

public sealed partial class DownloadConfirmationPage : Page
{
    private readonly DownloadForm form;
    public event Action<string>? Started;
    public event Action? Cancelled;
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
        form.StateChanged += () => { StartButton.Content = form.ActionText; StartButton.IsEnabled = !form.IsBusy; };
        form.LayoutChanged += () => LayoutChanged?.Invoke();
    }
    private async void Start(object sender, RoutedEventArgs e)
    {
        if (!await form.SubmitAsync()) return;
        if (form.CreatedTaskId is { } id) Started?.Invoke(id);
        else Cancelled?.Invoke();
    }
    private void Cancel(object sender, RoutedEventArgs e) => Cancelled?.Invoke();
    private void CancelShortcut(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args) { Cancelled?.Invoke(); args.Handled = true; }
    private void StartShortcut(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args) { if (StartButton.IsEnabled) Start(sender,new()); args.Handled = true; }
}
