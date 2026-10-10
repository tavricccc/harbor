using Microsoft.Windows.Storage.Pickers;

namespace Harbor.Services;

internal static class NativePickers
{
    internal static async Task<string?> FolderAsync(nint owner, string initialFolder = "")
    {
        var picker = new FolderPicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(owner));
        if (Directory.Exists(initialFolder))
            picker.SuggestedStartFolder = initialFolder;
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    internal static async Task<string[]> TorrentsAsync(nint owner)
    {
        var picker = new FileOpenPicker(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(owner));
        picker.FileTypeFilter.Add(".torrent");
        return (await picker.PickMultipleFilesAsync()).Select(file => file.Path).ToArray();
    }
}
