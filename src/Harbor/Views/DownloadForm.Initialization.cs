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
    private async void InitializeForm(object sender, RoutedEventArgs e)
    {
        Loaded -= InitializeForm; SetBusy(true);
        try
        {
            var config = (await core.GetAsync("config"))!; Destination.Text = config["downloadDir"]?.GetValue<string>() ?? "";
            requestOptions.Load(initial, config); Connections.Value = config["protocolConfig"]?["http"]?["connections"]?.GetValue<int>() ?? 8;
            DirectDownload.IsChecked = config["extra"]?["defaultDirectDownload"]?.GetValue<bool>() == true;
            if (CategoriesEditor.UsesCategories(config))
                foreach (var category in CategoriesEditor.Read(config)) Category.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.Path });
            CategoryLabel.Visibility = Category.Visibility = Category.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var prefs = UiPreferences.Load();
            if (prefs.RememberDownloadDirectory && prefs.LastDownloadDirectory.Length > 0) Destination.Text = prefs.LastDownloadDirectory;
            if (Destination.Text.Length == 0) Destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            ApplyInitial();
            var links = Links.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (initial is not null && links.Length == 1 && DirectDownload.IsChecked != true) await InspectAsync(links[0]);
            else if (links.Length > 1) SetAction(Strings.Format("Download.StartBatch", links.Length));
        }
        catch (Exception error) { ShowError(error); }
        finally { SetBusy(false); if (ConfigJson.Lines(Links.Text).Length == 0) Links.Focus(FocusState.Programmatic); else FileName.Focus(FocusState.Programmatic); }
    }
    private void ApplyInitial()
    {
        if (initial is null) return;
        Links.Text = initial["req"]?["url"]?.GetValue<string>() ?? "";
        lastInput = Links.Text;
        if (initial["opts"]?["path"]?.GetValue<string>() is { Length: > 0 } path) Destination.Text = path;
        FileName.Text = initial["opts"]?["name"]?.GetValue<string>() ?? "";
        if (initial["opts"]?["extra"]?["connections"] is JsonValue connections) Connections.Value = connections.GetValue<int>();
        if (initial["req"]?["extra"]?["header"] is JsonObject headers) Headers.Text = string.Join("\n", headers.Select(pair => $"{pair.Key}: {pair.Value}"));
    }
}
