using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.ViewModels;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed partial class ExtensionsPage : Page
{
    private readonly DownloadsViewModel vm;
    private readonly StackPanel installed = new() { Spacing = 20 };
    private readonly StackPanel store = new() { Spacing = 20 };
    private readonly InfoBar message = new() { IsClosable = true };
    private readonly TextBox url = new() { Header = Strings.Get("Extensions.InstallSource"), PlaceholderText = "https://github.com/owner/gopeed-extension" };
    private readonly AutoSuggestBox search = new() { PlaceholderText = Strings.Get("Extensions.SearchPlaceholder"), QueryIcon = new SymbolIcon(Symbol.Find), MinWidth = 200 };
    private readonly ComboBox sort = new() { Items = { Strings.Get("Extensions.MostStars"), Strings.Get("Extensions.MostInstalled"), Strings.Get("Extensions.RecentlyUpdated") }, SelectedIndex = 0 };
    private readonly Button more = new() { Content = Strings.Get("Common.LoadMore"), Visibility = Visibility.Collapsed };
    private readonly HashSet<string> identities = [];
    private int nextPage = 1;
    private bool loading;
    public ExtensionsPage(DownloadsViewModel viewModel, string? repository = null)
    {
        vm = viewModel; url.Text = repository ?? "";
        var panel = new StackPanel { Spacing = 16, MaxWidth = 800, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = Strings.Get("Extensions.Title"), Style = (Style)Application.Current.Resources["CompactPageTitleStyle"] }); panel.Children.Add(message);
        var tabs = new Pivot();
        var browse = new StackPanel { Spacing = 16 };
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { search, sort } };
        browse.Children.Add(commands); browse.Children.Add(store); browse.Children.Add(more);
        tabs.Items.Add(new PivotItem { Header = Strings.Get("Extensions.Store"), Content = browse });
        tabs.Items.Add(new PivotItem { Header = Strings.Get("Extensions.Installed"), Content = installed });
        panel.Children.Add(tabs);
        var installPanel = new StackPanel { Spacing = 12 }; installPanel.Children.Add(url);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var install = NativeButtons.Create(Strings.Get("Common.Install"), "\uE896", true); install.Click += async (_, _) => await Install(url.Text.Trim(), install); actions.Children.Add(install);
        var local = new Button { Content = Strings.Get("Extensions.ChooseFolder") }; local.Click += async (_, _) => { var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle); var picked = await picker.PickSingleFolderAsync(); if (picked is not null) await Install(picked.Path, local); }; actions.Children.Add(local);
        installPanel.Children.Add(actions); panel.Children.Insert(2, new Expander { Header = Strings.Get("Extensions.OtherSource"), IsExpanded = repository is not null, Content = installPanel, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        Content = new ScrollViewer { Content = panel, Padding = new Thickness(0,0,16,24), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled, HorizontalContentAlignment = HorizontalAlignment.Left };
        search.QuerySubmitted += async (_, _) => await LoadStore(true); sort.SelectionChanged += async (_, _) => { if (IsLoaded) await LoadStore(true); }; more.Click += async (_, _) => await LoadStore(false);
        Loaded += async (_, _) => { await ReloadInstalled(); await LoadStore(true); };
    }
    private async Task Install(string source, Button button)
    {
        button.IsEnabled = false;
        try { if (source.Length == 0) throw new FormatException(Strings.Get("Errors.ExtensionSource")); await vm.Core.SendAsync(HttpMethod.Post, "extensions", new JsonObject { ["url"] = source }); url.Text = ""; await ReloadInstalled(); message.Severity = InfoBarSeverity.Success; message.Message = Strings.Get("Extensions.InstallSuccess"); message.IsOpen = true; await LoadStore(true); }
        catch (Exception error) { Error(error); } finally { button.IsEnabled = true; }
    }
    private async Task LoadStore(bool reset)
    {
        if (loading) return; loading = true; more.IsEnabled = false;
        if (reset) { nextPage = 1; store.Children.Clear(); }
        try
        {
            var order = sort.SelectedIndex switch { 1 => "installs", 2 => "updated", _ => "stars" };
            var data = JsonNode.Parse(await vm.Core.FetchTextAsync($"https://gopeed.com/api/extensions?page={nextPage}&limit=20&sort={order}&order=desc&q={Uri.EscapeDataString(search.Text.Trim())}"))!;
            foreach (var node in data["data"]!.AsArray())
            {
                var ext = node!; var section = new StackPanel { Spacing = 8 };
                section.Children.Add(new TextBlock { Text = $"{ext["title"]}  {ext["version"]}", Style = (Style)Application.Current.Resources["CompactSectionTitleStyle"], TextWrapping = TextWrapping.Wrap });
                section.Children.Add(new TextBlock { Text = ext["description"]?.ToString() ?? "", TextWrapping = TextWrapping.Wrap });
                section.Children.Add(new TextBlock { Text = Strings.Format("Extensions.Stats", ext["author"], ext["stars"], ext["installCount"]), Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var identity = ext["author"] + "@" + ext["name"];
                var button = new Button { Content = identities.Contains(identity) ? Strings.Get("Extensions.Installed") : Strings.Get("Common.Install"), IsEnabled = !identities.Contains(identity) };
                var source = ext["repoUrl"]!.GetValue<string>(); if (ext["directory"]?.GetValue<string>() is { Length: > 0 } directory) source += "#" + directory;
                button.Click += async (_, _) => await Install(source, button); actions.Children.Add(button);
                AddLink(actions, Strings.Get("Extensions.Details"), ext["repoUrl"]?.GetValue<string>()); AddLink(actions, Strings.Get("Common.Website"), ext["homepage"]?.GetValue<string>());
                section.Children.Add(actions); store.Children.Add(section);
            }
            if (store.Children.Count == 0) store.Children.Add(new TextBlock { Text = Strings.Get("Extensions.NoMatches") });
            more.Visibility = data["pagination"]!["hasNext"]!.GetValue<bool>() ? Visibility.Visible : Visibility.Collapsed; nextPage++;
        }
        catch (Exception error) { Error(error); var retry = new Button { Content = Strings.Get("Extensions.Reload") }; retry.Click += async (_, _) => await LoadStore(true); if (store.Children.Count == 0) store.Children.Add(retry); }
        finally { loading = false; more.IsEnabled = true; }
    }
    private static void AddLink(StackPanel panel, string title, string? address) { if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http") panel.Children.Add(new HyperlinkButton { Content = title, NavigateUri = uri }); }
    private void Error(Exception error) { message.Severity = InfoBarSeverity.Error; message.Message = UserError.Message(error); message.IsOpen = true; }
}
