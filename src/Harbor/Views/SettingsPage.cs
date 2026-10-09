using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.ViewModels;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class SettingsPage : Page
{
    private readonly DownloadsViewModel vm;
    private readonly nint windowHandle;
    private readonly SettingsFields fields = new();
    private readonly InfoBar message = new() { IsClosable = true, Visibility = Visibility.Collapsed };
    private readonly Pivot sections = new();
    private Button saveButton = null!;
    private readonly ComboBox theme = new() { Header = Strings.Get("Settings.Theme"), HorizontalAlignment = HorizontalAlignment.Stretch, Items = { Strings.Get("Common.FollowWindows"), Strings.Get("Settings.Light"), Strings.Get("Settings.Dark") }, SelectedIndex = 0 };
    private readonly CheckBox remember = new() { Content = Strings.Get("Settings.RememberFolder") };
    private readonly CheckBox closeProgress = new() { Content = Strings.Get("Settings.CloseProgress") };
    private readonly CheckBox startup = new() { Content = Strings.Get("Settings.Startup") };
    private readonly CheckBox checkUpdates = new() { Content = Strings.Get("Settings.CheckUpdates") };
    private readonly NumberBox apiPort = new() { Header = Strings.Get("Settings.ApiPort"), Minimum = 1024, Maximum = 65535, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    private TextBox folder = null!;
    private TextBox customTrackers = null!;
    private readonly CategoriesEditor categories;
    private readonly MirrorsEditor mirrors = new();
    private readonly ComboBox proxyMode = new() { Header = Strings.Get("Proxy.Mode"), HorizontalAlignment = HorizontalAlignment.Stretch, Items = { Strings.Get("Common.FollowSystem"), Strings.Get("Request.Direct"), Strings.Get("Common.Custom") }, SelectedIndex = 0 };

    public event Action? CloseRequested;

    public SettingsPage(DownloadsViewModel viewModel, nint ownerHandle)
    {
        vm = viewModel;
        windowHandle = ownerHandle;
        categories = new CategoriesEditor(ownerHandle);
        BuildDownloads();
        BuildBehavior();
        BuildCategories();
        BuildNetwork();
        BuildAutomation();
        BuildAbout();

        var surface = new Grid { Padding = new Thickness(24, 16, 24, 20), RowSpacing = 16 };
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        surface.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel { Spacing = 16 };
        header.Children.Add(new TextBlock { Text = Strings.Get("Common.Settings"), Style = (Style)Application.Current.Resources["TitleTextBlockStyle"] });
        header.Children.Add(message);
        surface.Children.Add(header);
        NativeInfoBars.CollapseWhenClosed(message);
        Grid.SetRow(sections, 1);
        surface.Children.Add(sections);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var cancel = NativeButtons.Create(Strings.Get("Common.Cancel"), "\uE711");
        cancel.Click += (_, _) => CloseRequested?.Invoke();
        saveButton = NativeButtons.Create(Strings.Get("Common.Save"), "\uE74E", true);
        saveButton.Click += Save;
        sections.IsEnabled = saveButton.IsEnabled = false;
        actions.Children.Add(cancel);
        actions.Children.Add(saveButton);
        Grid.SetRow(actions, 2);
        surface.Children.Add(actions);
        Content = surface;
        Loaded += Load;
    }

    private StackPanel Section(string title)
    {
        var panel = new StackPanel { Spacing = 20, Padding = new Thickness(0, 12, 12, 16) };
        sections.Items.Add(new PivotItem
        {
            Header = title,
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            }
        });
        return panel;
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
            remember.IsChecked = prefs.RememberDownloadDirectory;
            closeProgress.IsChecked = prefs.CloseProgressAfterOpen;
            language.SelectedValue = prefs.Language;
            apiPort.Value = prefs.ApiPort;
            checkUpdates.IsChecked = prefs.CheckForUpdates;
            startup.IsChecked = WindowsIntegration.StartsWithWindows;
            proxyMode.SelectedIndex = config["proxy"]?["enable"]?.GetValue<bool>() == true ? config["proxy"]?["system"]?.GetValue<bool>() == true ? 0 : 2 : 1;
            theme.SelectedIndex = WindowAppearance.Theme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };
            sections.IsEnabled = saveButton.IsEnabled = true;
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
            prefs.RememberDownloadDirectory = remember.IsChecked == true;
            prefs.CloseProgressAfterOpen = closeProgress.IsChecked == true;
            prefs.ApiPort = (int)apiPort.Value;
            prefs.CheckForUpdates = checkUpdates.IsChecked == true;
            prefs.Save();
            WindowsIntegration.SetStartup(startup.IsChecked == true);
            var selectedTheme = theme.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            ((FrameworkElement)App.Window.Content).RequestedTheme = selectedTheme;
            ((FrameworkElement)XamlRoot.Content).RequestedTheme = selectedTheme;
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
