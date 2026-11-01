using Harbor.Controls;
using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Views;

/// <summary>
/// One download's options: a segmented tab bar over one settings card per topic (general, request, proxy,
/// BitTorrent, extraction), label on the left and its control on the right.
/// </summary>
internal sealed class DownloadOptionsPanel : StackPanel
{
    public SettingToggle SkipCheck { get; } = new() { Label = Strings.Get("Download.SkipCheck"), Description = Strings.Get("Download.SkipCheckHint") };
    public NumberBox Connections { get; } = new() { Value = 8, Minimum = 1, Maximum = 256, Width = 120, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
    public TextBox Headers { get; } = Multiline(Strings.Get("Download.HeadersPlaceholder"));
    private readonly ComboBox method = new() { IsEditable = true, Items = { "GET", "POST", "PUT", "HEAD" }, SelectedIndex = 0, Width = 140, MinWidth = 0 };
    private readonly TextBox body = Multiline("");
    private readonly SettingToggle skipCert = new() { Label = Strings.Get("Request.SkipCertificate") };
    private readonly TextBox trackers = Multiline("");
    private readonly Segmented proxyMode = Choices("Common.Default", "Request.DirectShort", "Common.Custom");
    private readonly ComboBox scheme = new() { Items = { "http", "https", "socks5" }, SelectedIndex = 0, Width = 140, MinWidth = 0 };
    private readonly TextBox host = new() { Width = 240, PlaceholderText = "127.0.0.1:7890" };
    private readonly TextBox user = new() { Width = 240 };
    private readonly PasswordBox password = new() { Width = 240 };
    private readonly Segmented autoTorrent = Choices("Common.Default", "Common.On", "Common.Off");
    private readonly Segmented deleteTorrent = Choices("Common.Default", "Common.On", "Common.Off");
    private readonly Segmented extract = Choices("Common.Default", "Common.On", "Common.Off");
    private readonly PasswordBox archivePassword = new() { Width = 240 };
    private readonly SettingToggle deleteArchive = new() { Label = Strings.Get("Archive.DeleteAfter") };
    private readonly SettingRow[] customProxy;
    private readonly SettingCard[] cards;
    public event Action? RequestChanged;
    public event Action? LayoutChanged;
    public DownloadOptionsPanel()
    {
        Spacing = 12;
        customProxy = [Row("Common.Protocol", scheme), Row("Proxy.Host", host), Row("Common.Username", user), Row("Common.Password", password)];
        cards =
        [
            Card(SkipCheck, Row("Download.Connections", Connections), Stacked("Common.HttpHeaders", Headers)),
            Card(Row("Request.Method", method), Stacked("Request.Body", body), skipCert),
            Card([Row("Request.ConnectionMode", proxyMode), .. customProxy]),
            Card(Stacked("Request.Trackers", trackers), Row("Torrent.AutoDownload", autoTorrent), Row("Torrent.DeleteFile", deleteTorrent)),
            Card(Row("Archive.AutoExtract", extract), Row("Archive.Password", archivePassword), deleteArchive),
        ];
        var tabs = new Segmented { HorizontalAlignment = HorizontalAlignment.Left, Options = string.Join("|", new[] { "Options.General", "Options.Request", "Options.Proxy" }.Select(Strings.Get).Append("BitTorrent").Append(Strings.Get("Options.Archive"))) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tabs, Strings.Get("Download.OptionsTitle"));
        tabs.SelectionChanged += (_, _) => { Show(tabs.SelectedIndex); LayoutChanged?.Invoke(); };
        Children.Add(tabs);
        foreach (var card in cards)
            Children.Add(card);
        Show(0);
        ShowCustomProxy();
        method.SelectionChanged += (_, _) => RequestChanged?.Invoke();
        method.TextSubmitted += (_, _) => RequestChanged?.Invoke();
        body.TextChanged += (_, _) => RequestChanged?.Invoke();
        trackers.TextChanged += (_, _) => RequestChanged?.Invoke();
        proxyMode.SelectionChanged += (_, _) => { ShowCustomProxy(); RequestChanged?.Invoke(); LayoutChanged?.Invoke(); };
        host.TextChanged += (_, _) => RequestChanged?.Invoke();
        user.TextChanged += (_, _) => RequestChanged?.Invoke();
        password.PasswordChanged += (_, _) => RequestChanged?.Invoke();
        scheme.SelectionChanged += (_, _) => RequestChanged?.Invoke();
        skipCert.RegisterPropertyChangedCallback(SettingToggle.IsOnProperty, (_, _) => RequestChanged?.Invoke());
    }
    private void Show(int index)
    {
        for (int i = 0; i < cards.Length; i++)
            cards[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ShowCustomProxy()
    {
        foreach (var row in customProxy)
            row.Visibility = proxyMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    }
    private static SettingCard Card(params SettingRow[] rows)
    {
        var card = new SettingCard();
        foreach (var row in rows)
            card.Children.Add(row);
        return card;
    }
    private static SettingRow Row(string label, UIElement control) => new() { Label = Strings.Get(label), Control = control };
    private static SettingRow Stacked(string label, UIElement control) => new() { Label = Strings.Get(label), Control = control, Stacked = true };
    private static TextBox Multiline(string placeholder) => new() { PlaceholderText = placeholder, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxHeight = 140 };
    private static Segmented Choices(params string[] keys) => new() { Options = string.Join("|", keys.Select(Strings.Get)) };
    public void Load(JsonObject? initial, JsonNode config)
    {
        var req = initial?["req"];
        method.Text = req?["extra"]?["method"]?.GetValue<string>() ?? "GET";
        body.Text = req?["extra"]?["body"]?.GetValue<string>() ?? "";
        skipCert.IsOn = req?["skipVerifyCert"]?.GetValue<bool>() == true;
        trackers.Text = string.Join("\n", req?["extra"]?["trackers"]?.AsArray().Select(x => x!.GetValue<string>()) ?? []);
        proxyMode.SelectedIndex = req?["proxy"]?["mode"]?.GetValue<string>() switch
        {
            "none" => 1,
            "custom" => 2,
            _ => 0
        };
        scheme.SelectedIndex = req?["proxy"]?["scheme"]?.GetValue<string>() switch
        {
            "https" => 1,
            "socks5" => 2,
            _ => 0
        };
        host.Text = req?["proxy"]?["host"]?.GetValue<string>() ?? "";
        user.Text = req?["proxy"]?["usr"]?.GetValue<string>() ?? "";
        password.Password = req?["proxy"]?["pwd"]?.GetValue<string>() ?? "";
        foreach (var (control, key) in new[] { (autoTorrent, "autoTorrent"), (deleteTorrent, "deleteTorrentAfterDownload"), (extract, "autoExtract") })
            control.SelectedIndex = initial?["opts"]?["extra"]?[key] is JsonValue value ? value.GetValue<bool>() ? 1 : 2 : 0;
        archivePassword.Password = initial?["opts"]?["extra"]?["archivePassword"]?.GetValue<string>() ?? "";
        deleteArchive.IsOn = initial?["opts"]?["extra"]?["deleteAfterExtract"]?.GetValue<bool>() ?? config["archive"]?["deleteAfterExtract"]?.GetValue<bool>() ?? false;
    }
    public void Apply(JsonObject request)
    {
        var req = request["req"]!.AsObject();
        req["extra"] ??= new JsonObject();
        var extra = req["extra"]!.AsObject();
        extra["method"] = method.Text.Trim().Length == 0 ? "GET" : method.Text.Trim().ToUpperInvariant();
        extra["body"] = body.Text;
        if (trackers.Text.Length > 0)
            extra["trackers"] = ConfigJson.Array(ConfigJson.Lines(trackers.Text));
        req["skipVerifyCert"] = skipCert.IsOn;
        if (proxyMode.SelectedIndex == 2 && host.Text.Trim().Length == 0)
            throw new FormatException(Strings.Get("Errors.ProxyHost"));
        req["proxy"] = new JsonObject { ["mode"] = proxyMode.SelectedIndex switch { 1 => "none", 2 => "custom", _ => "follow" }, ["scheme"] = scheme.SelectedItem?.ToString() ?? "http", ["host"] = host.Text.Trim(), ["usr"] = user.Text, ["pwd"] = password.Password };
        var opts = request["opts"]!.AsObject();
        opts["extra"] ??= new JsonObject();
        foreach (var (control, key) in new[] { (autoTorrent, "autoTorrent"), (deleteTorrent, "deleteTorrentAfterDownload"), (extract, "autoExtract") })
            opts["extra"]![key] = control.SelectedIndex == 0 ? null : JsonValue.Create(control.SelectedIndex == 1);
        opts["extra"]!["archivePassword"] = archivePassword.Password;
        opts["extra"]!["deleteAfterExtract"] = deleteArchive.IsOn;
    }
}
