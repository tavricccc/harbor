using Microsoft.UI.Xaml;

namespace Harbor.Models;

// WinUI row bindings stay separate from the engine task model.
public sealed partial class DownloadItem
{
    public Visibility ProgressVisibility => !IsComplete && Size > 0 && Status != "error" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActionVisibility => CanAct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FolderVisibility => IsComplete && !IsProcessing ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SeedVisibility => Uploading ? Visibility.Visible : Visibility.Collapsed;
}
