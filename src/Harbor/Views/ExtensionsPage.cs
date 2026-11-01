using Harbor.Controls;
using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using Harbor.ViewModels;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

/// <summary>
/// Extensions: the title with 「Install from another source」 on the right, a segmented switch between the store and
/// the installed list, the store's search and sort, then one card per extension. Loading, empty and failed lists show
/// as one note above the cards.
/// </summary>
public sealed partial class ExtensionsPage : Page
{
    private readonly DownloadsViewModel vm;
    private readonly string? repository;
    private readonly Segmented tabs = new() { Options = string.Join("|", Strings.Get("Extensions.Store"), Strings.Get("Extensions.Installed")), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel store = new() { Spacing = 8 };
    private readonly StackPanel installed = new() { Spacing = 8, Visibility = Visibility.Collapsed };
    private readonly StackPanel toolbar;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly InfoBar message = new() { IsClosable = true };
    private readonly TextBox search = new() { PlaceholderText = Strings.Get("Extensions.SearchPlaceholder"), MinWidth = 240 };
    private readonly Segmented sort = new() { Options = string.Join("|", Strings.Get("Extensions.MostStars"), Strings.Get("Extensions.MostInstalled"), Strings.Get("Extensions.RecentlyUpdated")) };
    private readonly Button more = new() { Content = Strings.Get("Common.LoadMore"), HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
    private readonly HashSet<string> identities = [];
    private int nextPage = 1;
    private bool loading;

    public ExtensionsPage(DownloadsViewModel viewModel, string? repository = null)
    {
        vm = viewModel;
        this.repository = repository;
        NativeInfoBars.CollapseWhenClosed(message);
        status.Style = (Style)Application.Current.Resources["UiNote"];
        var other = new Button { Content = Strings.Get("Extensions.OtherSource") + "…", VerticalAlignment = VerticalAlignment.Center };
        other.Click += async (_, _) => await InstallFromSource(null);
        var title = new Grid();
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.Children.Add(new TextBlock { Text = Strings.Get("Extensions.Title"), Style = (Style)Application.Current.Resources["UiSectionTitle"] });
        Grid.SetColumn(other, 1); title.Children.Add(other);
        toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { search, sort } };

        var panel = new StackPanel { Spacing = 16, MaxWidth = 800, HorizontalAlignment = HorizontalAlignment.Stretch, Children = { title, message, tabs, toolbar, status, store, installed, more } };
        Content = new ScrollViewer { Content = panel, Padding = new Thickness(0, 0, 16, 24), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled, HorizontalContentAlignment = HorizontalAlignment.Left };
        tabs.SelectionChanged += (_, _) => ShowTab();
        search.KeyDown += async (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; await LoadStore(true); } };
        sort.SelectionChanged += async (_, _) => { if (IsLoaded) await LoadStore(true); };
        more.Click += async (_, _) => await LoadStore(false);
        Loaded += async (_, _) =>
        {
            await ReloadInstalled();
            await LoadStore(true);
            // A gopeed:// link with a repository opens the install dialog filled in.
            if (this.repository is { Length: > 0 } link) await InstallFromSource(link);
        };
    }

    private bool StoreShown => tabs.SelectedIndex == 0;

    private void ShowTab()
    {
        store.Visibility = toolbar.Visibility = StoreShown ? Visibility.Visible : Visibility.Collapsed;
        installed.Visibility = StoreShown ? Visibility.Collapsed : Visibility.Visible;
        more.Visibility = StoreShown && hasMore ? Visibility.Visible : Visibility.Collapsed;
        Status(StoreShown ? storeStatus : installedStatus);
    }

    private bool hasMore;
    private (string Text, bool Failed)? storeStatus, installedStatus;

    /// <summary>The note above the list for the tab on screen: loading, empty or failed (in the danger color).</summary>
    private void Status((string Text, bool Failed)? value)
    {
        status.Text = value?.Text ?? "";
        status.Visibility = value is null ? Visibility.Collapsed : Visibility.Visible;
        status.Foreground = Motion.Resource(status, value?.Failed == true ? "UiDangerBrush" : "UiText3Brush");
    }

    private void SetStoreStatus((string, bool)? value) { storeStatus = value; if (StoreShown) Status(value); }
    private void SetInstalledStatus((string, bool)? value) { installedStatus = value; if (!StoreShown) Status(value); }

    private async Task InstallFromSource(string? prefill)
    {
        var url = new TextBox { Header = Strings.Get("Extensions.InstallSource"), Text = prefill ?? "", PlaceholderText = "https://github.com/owner/gopeed-extension" };
        var folder = new Button { Content = Strings.Get("Extensions.ChooseFolder") };
        var errorBar = new InfoBar { Severity = InfoBarSeverity.Error };
        folder.Click += async (_, _) =>
        {
            try { if (await NativePickers.FolderAsync(App.WindowHandle) is { } path) url.Text = path; }
            catch (Exception error) { errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; }
        };
        var dialog = new ContentDialog
        {
            Title = Strings.Get("Extensions.OtherSource"),
            Content = new StackPanel { Spacing = 12, MinWidth = 420, Children = { errorBar, url, folder } },
            PrimaryButtonText = Strings.Get("Common.Install"),
            CloseButtonText = Strings.Get("Common.Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += async (_, click) =>
        {
            var deferral = click.GetDeferral();
            try { await InstallCore(url.Text.Trim()); }
            catch (Exception error) { click.Cancel = true; errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; }
            finally { deferral.Complete(); }
        };
        if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
        {
            Success(Strings.Get("Extensions.InstallSuccess"));
            tabs.SelectedIndex = 1;
        }
    }

    private async Task InstallCore(string source)
    {
        if (source.Length == 0)
            throw new FormatException(Strings.Get("Errors.ExtensionSource"));
        await vm.Core.SendAsync(HttpMethod.Post, "extensions", new JsonObject { ["url"] = source });
        await ReloadInstalled();
        await LoadStore(true);
    }

    private async Task Install(string source, Button button)
    {
        button.IsEnabled = false;
        try
        {
            await InstallCore(source);
            Success(Strings.Get("Extensions.InstallSuccess"));
        }
        catch (Exception error) { Error(error); button.IsEnabled = true; }
    }

    private async Task LoadStore(bool reset)
    {
        if (loading)
            return;
        loading = true;
        more.IsEnabled = false;
        if (reset)
        {
            nextPage = 1;
            store.Children.Clear();
            SetStoreStatus((Strings.Get("Extensions.Loading"), false));
        }
        try
        {
            var order = sort.SelectedIndex switch
            {
                1 => "installs",
                2 => "updated",
                _ => "stars"
            };
            var data = JsonNode.Parse(await vm.Core.FetchTextAsync($"https://gopeed.com/api/extensions?page={nextPage}&limit=20&sort={order}&order=desc&q={Uri.EscapeDataString(search.Text.Trim())}"))!;
            foreach (var node in data["data"]!.AsArray())
                store.Children.Add(StoreCard(node!));
            SetStoreStatus(store.Children.Count == 0 ? (Strings.Get("Extensions.NoMatches"), false) : null);
            hasMore = data["pagination"]!["hasNext"]!.GetValue<bool>();
            more.Visibility = hasMore && StoreShown ? Visibility.Visible : Visibility.Collapsed;
            nextPage++;
        }
        catch (Exception error)
        {
            SetStoreStatus((Strings.Format("Extensions.LoadFailed", UserError.Message(error)), true));
            if (store.Children.Count == 0)
            {
                var retry = new Button { Content = Strings.Get("Extensions.Reload") };
                retry.Click += async (_, _) => await LoadStore(true);
                store.Children.Add(retry);
            }
        }
        finally { loading = false; more.IsEnabled = true; }
    }

    private Border StoreCard(JsonNode ext)
    {
        var identity = ext["author"] + "@" + ext["name"];
        FrameworkElement action;
        // Installed shows as a quiet label, not a disabled button.
        if (identities.Contains(identity))
            action = new TextBlock { Text = Strings.Get("Extensions.Installed"), Style = (Style)Application.Current.Resources["UiNote"], VerticalAlignment = VerticalAlignment.Center };
        else
        {
            var button = new Button { Content = Strings.Get("Common.Install"), VerticalAlignment = VerticalAlignment.Top };
            var source = ext["repoUrl"]!.GetValue<string>();
            if (ext["directory"]?.GetValue<string>() is { Length: > 0 } directory)
                source += "#" + directory;
            button.Click += async (_, _) => await Install(source, button);
            action = button;
        }
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        AddLink(links, Strings.Get("Extensions.Details"), ext["repoUrl"]?.GetValue<string>());
        AddLink(links, Strings.Get("Common.Website"), ext["homepage"]?.GetValue<string>());
        return Card(ext["title"]?.ToString() ?? "", ext["version"]?.ToString() ?? "", ext["description"]?.ToString() ?? "",
            Strings.Format("Extensions.Stats", ext["author"], ext["stars"], ext["installCount"]), action, links);
    }

    /// <summary>One extension: a 40 icon tile, name and version, up to two lines of description, a note line,
    /// optional rows below; the main control on the right.</summary>
    private static Border Card(string name, string version, string description, string? note, FrameworkElement action, params UIElement[] below)
    {
        var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Top, Child = new Icon { Glyph = "app", Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Motion.Themed(tile, Border.BackgroundProperty, "UiFillSelectedBrush");
        var title = new TextBlock { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        title.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = name });
        title.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "  " + version, FontWeight = Microsoft.UI.Text.FontWeights.Normal, FontSize = 12 });
        Motion.Themed(title, TextBlock.ForegroundProperty, "UiText1Brush");
        var text = new StackPanel { Spacing = 4, Children = { title } };
        if (description.Length > 0)
            text.Children.Add(new TextBlock { Text = description, Style = (Style)Application.Current.Resources["UiBody"], TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        if (note is { Length: > 0 })
            text.Children.Add(new TextBlock { Text = note, Style = (Style)Application.Current.Resources["UiNote"], TextWrapping = TextWrapping.Wrap });
        foreach (var row in below)
            if (row is not Panel { Children.Count: 0 })
                text.Children.Add(row);
        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(tile);
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        Grid.SetColumn(action, 2); grid.Children.Add(action);
        return BrowserIntegrationGuide.Card(grid, new Thickness(14, 12, 14, 12));
    }

    private static void AddLink(StackPanel panel, string title, string? address)
    {
        if (Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            panel.Children.Add(new HyperlinkButton { Content = title, NavigateUri = uri, Padding = new Thickness(0, 2, 8, 2), FontSize = 12 });
    }

    private void Success(string text)
    {
        message.Severity = InfoBarSeverity.Success;
        message.Message = text;
        message.IsOpen = true;
    }

    private void Error(Exception error)
    {
        message.Severity = InfoBarSeverity.Error;
        message.Message = UserError.Message(error);
        message.IsOpen = true;
    }
}
