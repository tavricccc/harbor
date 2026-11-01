using Harbor.Controls;
using Harbor.Localization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.ViewModels;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

/// <summary>
/// Pane's settings window: a sidebar of sections (the selection slides behind them) beside one section's cards;
/// Reload on the left of the footer, Cancel and Save on the right.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly DownloadsViewModel vm;
    private readonly nint windowHandle;
    private readonly SettingsFields fields = new();
    private readonly InfoBar message = new() { IsClosable = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
    private readonly StackPanel navigation = new() { Spacing = 2 };
    private readonly Canvas highlightLayer = new() { IsHitTestVisible = false };
    private readonly SlidingHighlight highlight;
    private readonly ScrollViewer content = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Disabled };
    private readonly StackPanel contentHost = new() { Padding = new Thickness(8, 0, 24, 24) };
    private readonly List<(Button Item, StackPanel Panel)> sections = [];
    private int selected = -1;
    private Button saveButton = null!;
    private readonly Segmented theme = new() { Options = string.Join("|", Strings.Get("Common.FollowWindows"), Strings.Get("Settings.Light"), Strings.Get("Settings.Dark")) };
    private readonly SettingToggle remember = new() { Label = Strings.Get("Settings.RememberFolder") };
    private readonly SettingToggle closeProgress = new() { Label = Strings.Get("Settings.CloseProgress") };
    private readonly SettingToggle startup = new() { Label = Strings.Get("Settings.Startup") };
    private readonly SettingToggle checkUpdates = new() { Label = Strings.Get("Settings.CheckUpdates") };
    private readonly NumberBox apiPort = new() { Width = 140, MinWidth = 0, Minimum = 1024, Maximum = 65535, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    private TextBox folder = null!;
    private TextBox customTrackers = null!;
    private readonly CategoriesEditor categories;
    private readonly MirrorsEditor mirrors = new();
    private readonly Segmented proxyMode = new() { Options = string.Join("|", Strings.Get("Common.FollowSystem"), Strings.Get("Request.DirectShort"), Strings.Get("Common.Custom")) };

    public event Action? CloseRequested;
    public event Action? MinimizeRequested;
    internal FrameworkElement TitleBar { get; private set; } = null!;

    public SettingsPage(DownloadsViewModel viewModel, nint ownerHandle)
    {
        vm = viewModel;
        windowHandle = ownerHandle;
        categories = new CategoriesEditor(ownerHandle);
        highlight = new SlidingHighlight(highlightLayer, 10);
        BuildDownloads();
        BuildBehavior();
        BuildCategories();
        BuildNetwork();
        BuildAutomation();
        BuildAbout();

        var surface = new Grid();
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var title = new WindowHeader { Title = Strings.Get("Common.Settings") };
        title.MinimizeRequested += () => MinimizeRequested?.Invoke();
        title.CloseRequested += () => CloseRequested?.Invoke();
        TitleBar = title;
        Grid.SetColumnSpan(title, 2);
        surface.Children.Add(title);

        var sidebar = new Grid { Padding = new Thickness(12, 4, 12, 12) };
        sidebar.Children.Add(highlightLayer);
        sidebar.Children.Add(new StackPanel { Children = { navigation } });
        Grid.SetRow(sidebar, 1);
        surface.Children.Add(sidebar);

        NativeInfoBars.CollapseWhenClosed(message);
        content.Content = contentHost;
        Grid.SetRow(content, 1);
        Grid.SetColumn(content, 1);
        surface.Children.Add(content);

        var footer = new Grid { Padding = new Thickness(20, 12, 20, 12), BorderThickness = new Thickness(0, 1, 0, 0) };
        Motion.Themed(footer, Grid.BorderBrushProperty, "PaneStrokeBrush");
        var reload = NativeButtons.Create(Strings.Get("Settings.Reload"));
        reload.Click += (_, _) => Load(this, new RoutedEventArgs());
        footer.Children.Add(reload);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = NativeButtons.Create(Strings.Get("Common.Cancel"));
        cancel.Click += (_, _) => CloseRequested?.Invoke();
        saveButton = NativeButtons.Create(Strings.Get("Common.Save"), true);
        saveButton.Click += Save;
        content.IsEnabled = saveButton.IsEnabled = false;
        actions.Children.Add(cancel);
        actions.Children.Add(saveButton);
        footer.Children.Add(actions);
        Grid.SetRow(footer, 2);
        Grid.SetColumnSpan(footer, 2);
        surface.Children.Add(footer);
        Content = surface;
        Select(0, false);
        ActualThemeChanged += (_, _) => PaintNavigation();
        Loaded += Load;
    }

    private StackPanel Section(string title, string icon)
    {
        var panel = new StackPanel();
        var label = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center };
        var item = new Button
        {
            Style = (Style)Application.Current.Resources["PaneNavItem"],
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new Icon { Glyph = icon, Width = 16, Height = 16 }, label } }
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, title);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, "SettingsSection-" + icon);
        int index = sections.Count;
        item.Click += (_, _) => Select(index, true);
        item.SizeChanged += (_, _) => { if (index == selected) highlight.MoveTo(item, false); };
        navigation.Children.Add(item);
        sections.Add((item, panel));
        return panel;
    }

    /// <summary>Opens the last section, About, where updates are checked and installed.</summary>
    public void ShowAbout() => Select(sections.Count - 1, false);

    private void Select(int index, bool animate)
    {
        if (index == selected)
            return;
        selected = index;
        PaintNavigation();
        contentHost.Children.Clear();
        contentHost.Children.Add(message);
        contentHost.Children.Add(sections[index].Panel);
        content.ChangeView(null, 0, null, true);
        if (sections[index].Item.ActualWidth > 0)
            highlight.MoveTo(sections[index].Item, animate);
    }

    // The selected section reads primary and semibold, the rest secondary.
    private void PaintNavigation()
    {
        for (int i = 0; i < sections.Count; i++)
        {
            var item = sections[i].Item;
            item.Foreground = Motion.Resource(item, i == selected ? "PaneText1Brush" : "PaneText2Brush");
            item.FontWeight = i == selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private async void Load(object sender, RoutedEventArgs args)
    {
        Loaded -= Load;
        try
        {
            var config = (await vm.Core.GetAsync("config"))!.AsObject();
            fields.Load(config);
            categories.Load(config);
            mirrors.Load(config);
            var prefs = UiPreferences.Load();
            remember.IsOn = prefs.RememberDownloadDirectory;
            closeProgress.IsOn = prefs.CloseProgressAfterOpen;
            language.SelectedValue = prefs.Language;
            apiPort.Value = prefs.ApiPort;
            checkUpdates.IsOn = prefs.CheckForUpdates;
            startup.IsOn = WindowsIntegration.StartsWithWindows;
            proxyMode.SelectedIndex = config["proxy"]?["enable"]?.GetValue<bool>() == true ? config["proxy"]?["system"]?.GetValue<bool>() == true ? 0 : 2 : 1;
            theme.SelectedIndex = WindowAppearance.Theme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
            content.IsEnabled = saveButton.IsEnabled = true;
        }
        catch (Exception error) { Report(error); }
    }

    private async void Save(object sender, RoutedEventArgs args)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            if (!Path.IsPathFullyQualified(folder.Text))
                throw new FormatException(Strings.Get("Errors.DownloadFolder"));
            if (!double.IsFinite(apiPort.Value))
                throw new FormatException(Strings.Get("Errors.Port"));
            var config = (await vm.Core.GetAsync("config"))!.AsObject();
            fields.Save(config);
            categories.Save(config);
            mirrors.Save(config);
            config["proxy"]!["enable"] = proxyMode.SelectedIndex != 1;
            config["proxy"]!["system"] = proxyMode.SelectedIndex == 0;
            if (proxyMode.SelectedIndex == 2 && string.IsNullOrWhiteSpace(config["proxy"]?["host"]?.GetValue<string>()))
                throw new FormatException(Strings.Get("Errors.ProxyHost"));
            if (proxyMode.SelectedIndex == 2 && config["proxy"]?["scheme"]?.GetValue<string>() is not ("http" or "https" or "socks5"))
                throw new FormatException(Strings.Get("Errors.ProxyScheme"));
            if (config["webhook"]?["enable"]?.GetValue<bool>() == true)
            {
                var urls = config["webhook"]!["urls"]!.AsArray();
                if (urls.Count == 0 || urls.Any(x => !Uri.TryCreate(x!.GetValue<string>(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
                    throw new FormatException(Strings.Get("Errors.WebhookUrl"));
            }
            if (config["script"]?["enable"]?.GetValue<bool>() == true && (config["script"]!["paths"]!.AsArray().Count == 0 || config["script"]!["paths"]!.AsArray().Any(x => !File.Exists(x!.GetValue<string>()))))
                throw new FormatException(Strings.Get("Errors.ScriptPath"));
            var subscribed = ConfigJson.Get(config, "extra.bt.subscribeTrackers")?.AsArray().Select(x => x!.GetValue<string>()) ?? [];
            ConfigJson.Set(config, "protocolConfig.bt.trackers", ConfigJson.Array(ConfigJson.Lines(customTrackers.Text).Concat(subscribed).Distinct()));
            Directory.CreateDirectory(folder.Text);
            await vm.Core.SendAsync(HttpMethod.Put, "config", config);
            var prefs = UiPreferences.Load();
            var portChanged = prefs.ApiPort != (int)apiPort.Value;
            var selectedLanguage = (string)language.SelectedValue;
            var languageChanged = prefs.Language != selectedLanguage;
            prefs.Language = selectedLanguage;
            prefs.RememberDownloadDirectory = remember.IsOn;
            prefs.CloseProgressAfterOpen = closeProgress.IsOn;
            prefs.ApiPort = (int)apiPort.Value;
            prefs.CheckForUpdates = checkUpdates.IsOn;
            prefs.Save();
            WindowsIntegration.SetStartup(startup.IsOn);
            var selectedTheme = theme.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            foreach (var window in Glass.GlassWindow.All)
                window.Content.RequestedTheme = selectedTheme;
            await File.WriteAllTextAsync(Path.Combine(CoreClient.DataDirectory, "theme.txt"), theme.SelectedIndex.ToString());
            if (portChanged || languageChanged)
                await NativeDialogs.ShowAsync(new ContentDialog
                {
                    Title = Strings.Get("Settings.Saved"),
                    Content = Strings.Get(portChanged && languageChanged ? "Settings.RestartLanguagePort" : languageChanged ? "Settings.RestartLanguage" : "Settings.RestartPort"),
                    CloseButtonText = Strings.Get("Common.GotIt")
                }, XamlRoot);
            CloseRequested?.Invoke();
        }
        catch (Exception error) { Report(error); }
        finally { button.IsEnabled = true; }
    }

    private void Success(string text)
    {
        message.Severity = InfoBarSeverity.Success;
        message.Message = text;
        message.IsOpen = true;
    }
    private void Report(Exception error)
    {
        message.Severity = InfoBarSeverity.Error;
        message.Message = UserError.Message(error);
        message.IsOpen = true;
    }
}
