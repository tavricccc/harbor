using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;
using Harbor.Services;

namespace Harbor;

public sealed partial class MainPage
{
    private void PasteShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox || !ViewModel.IsConnected)
            return;
        PasteDownload(sender, new RoutedEventArgs());
        args.Handled = true;
    }
    private async void OpenTorrent(object sender, RoutedEventArgs args)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".torrent");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
            var files = await picker.PickMultipleFilesAsync();
            if (files.Count == 0)
                return;
            await AddDownloadAsync(new JsonObject { ["req"] = new JsonObject { ["url"] = string.Join("\n", files.Select(file => file.Path)) } });
        }
        catch (Exception error) { ViewModel.Error = UserError.Message(error); }
    }
}
