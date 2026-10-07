using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Services;

public static class NativeDialogs
{
    public static async Task<ContentDialogResult> ShowAsync(ContentDialog dialog, XamlRoot root)
    {
        dialog.XamlRoot = root;
        dialog.RequestedTheme = ((FrameworkElement)root.Content).ActualTheme;
        return await dialog.ShowAsync();
    }
}
