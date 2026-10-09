using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.Models;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed partial class DownloadForm
{
    private async void PickFolder(object s, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            Destination.Text = folder.Path;
    }
    private async void PickTorrent(object s, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".torrent");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
            Links.Text = string.Join("\n", files.Select(file => file.Path));
    }
    private IEnumerable<string> linksForHistory() => ConfigJson.Lines(Links.Text).Where(x => !x.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
    private void SelectAllFiles(object sender, RoutedEventArgs e) => Files.SelectAll();
    private void SelectNoFiles(object sender, RoutedEventArgs e) => Files.SelectedItems.Clear();
    private async void ShowRecent(object sender, RoutedEventArgs e)
    {
        var history = new ListView { ItemsSource = UiPreferences.Load().RecentLinks, MaxHeight = 340, SelectionMode = ListViewSelectionMode.Single };
        var clear = new Button { Content = Strings.Get("Download.ClearRecent") };
        clear.Click += (_, _) => { var prefs = UiPreferences.Load(); prefs.RecentLinks.Clear(); prefs.Save(); history.ItemsSource = prefs.RecentLinks; };
        var dialog = new ContentDialog { Title = Strings.Get("Download.RecentLinks"), Content = new StackPanel { Spacing = 12, Children = { history, clear } }, PrimaryButtonText = Strings.Get("Download.UseLink"), CloseButtonText = Strings.Get("Common.Cancel"), IsPrimaryButtonEnabled = false };
        history.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = history.SelectedItem is not null;
        if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
            Links.Text = (string)history.SelectedItem;
    }
}
