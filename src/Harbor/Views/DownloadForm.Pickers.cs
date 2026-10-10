using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.Models;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class DownloadForm
{
    private async void PickFolder(object s, RoutedEventArgs e)
    {
        var button = (Button)s;
        button.IsEnabled = false;
        try
        {
            if (await NativePickers.FolderAsync(owner, Destination.Text) is { } path)
                Destination.Text = path;
        }
        catch (Exception error) { ShowError(error); }
        finally { button.IsEnabled = true; }
    }
    private async void PickTorrent(object s, RoutedEventArgs e)
    {
        var button = (Button)s;
        button.IsEnabled = false;
        try
        {
            var files = await NativePickers.TorrentsAsync(owner);
            if (files.Length > 0)
                Links.Text = string.Join("\n", files);
        }
        catch (Exception error) { ShowError(error); }
        finally { button.IsEnabled = true; }
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
