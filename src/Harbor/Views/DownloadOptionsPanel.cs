using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Views;

internal sealed class DownloadOptionsPanel : StackPanel
{
    private readonly ComboBox method = new() { Header = Strings.Get("Request.Method"), IsEditable = true, Items = { "GET", "POST", "PUT", "HEAD" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox body = new() { Header = Strings.Get("Request.Body"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxHeight = 160 };
    private readonly CheckBox skipCert = new() { Content = Strings.Get("Request.SkipCertificate") };
    private readonly TextBox trackers = new() { Header = Strings.Get("Request.Trackers"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64, MaxHeight = 160 };
    private readonly ComboBox proxyMode = new() { Header = Strings.Get("Request.ConnectionMode"), Items = { Strings.Get("Common.UseDefault"), Strings.Get("Request.Direct"), Strings.Get("Common.Custom") }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox scheme = new() { Header = Strings.Get("Common.Protocol"), Items = { "http", "https", "socks5" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox host = new() { Header = Strings.Get("Proxy.Host") };
    private readonly TextBox user = new() { Header = Strings.Get("Common.Username") };
    private readonly PasswordBox password = new() { Header = Strings.Get("Common.Password") };
    private readonly ComboBox autoTorrent = Choice(Strings.Get("Torrent.AutoDownload"));
    private readonly ComboBox deleteTorrent = Choice(Strings.Get("Torrent.DeleteFile"));
    private readonly ComboBox extract = Choice(Strings.Get("Archive.AutoExtract"));
    private readonly PasswordBox archivePassword = new() { Header = Strings.Get("Archive.Password") };
    private readonly CheckBox deleteArchive = new() { Content = Strings.Get("Archive.DeleteAfter") };
    private readonly StackPanel customProxy = new() { Spacing = 12, Visibility = Visibility.Collapsed };
    public event Action? RequestChanged;
    public DownloadOptionsPanel()
    {
        Spacing = 20;
        Children.Add(Section("HTTP", method, body, skipCert));
        Children.Add(Section("BitTorrent", trackers, autoTorrent, deleteTorrent));
        customProxy.Children.Add(Pair(scheme, host));
        customProxy.Children.Add(Pair(user, password));
        Children.Add(Section(Strings.Get("Proxy.Title"), proxyMode, customProxy));
        Children.Add(Section(Strings.Get("Archive.Extraction"), Pair(extract, archivePassword), deleteArchive));
        method.SelectionChanged += (_, _) => RequestChanged?.Invoke(); method.TextSubmitted += (_, _) => RequestChanged?.Invoke();
        body.TextChanged += (_, _) => RequestChanged?.Invoke(); trackers.TextChanged += (_, _) => RequestChanged?.Invoke();
        proxyMode.SelectionChanged += (_, _) => { customProxy.Visibility = proxyMode.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed; RequestChanged?.Invoke(); };
        host.TextChanged += (_, _) => RequestChanged?.Invoke(); user.TextChanged += (_, _) => RequestChanged?.Invoke(); password.PasswordChanged += (_, _) => RequestChanged?.Invoke(); scheme.SelectionChanged += (_, _) => RequestChanged?.Invoke();
        skipCert.Checked += (_, _) => RequestChanged?.Invoke(); skipCert.Unchecked += (_, _) => RequestChanged?.Invoke();
    }
    private static Expander Section(string title, params UIElement[] controls)
    {
        var section = new StackPanel { Spacing = 12 };
        foreach (var control in controls) section.Children.Add(control);
        return new Expander { Header = title, Content = section, HorizontalAlignment = HorizontalAlignment.Stretch };
    }
    private static Grid Pair(FrameworkElement first, FrameworkElement second)
    {
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(first);
        Grid.SetColumn(second, 1);
        row.Children.Add(second);
        return row;
    }
    private static ComboBox Choice(string title) => new() { Header = title, Items = { Strings.Get("Common.UseDefault"), Strings.Get("Common.Enable"), Strings.Get("Common.Disable") }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    public void Load(JsonObject? initial, JsonNode config)
    {
        var req = initial?["req"]; method.Text = req?["extra"]?["method"]?.GetValue<string>() ?? "GET"; body.Text = req?["extra"]?["body"]?.GetValue<string>() ?? "";
        skipCert.IsChecked = req?["skipVerifyCert"]?.GetValue<bool>() == true;
        trackers.Text = string.Join("\n", req?["extra"]?["trackers"]?.AsArray().Select(x => x!.GetValue<string>()) ?? []);
        proxyMode.SelectedIndex = req?["proxy"]?["mode"]?.GetValue<string>() switch { "none" => 1, "custom" => 2, _ => 0 };
        scheme.SelectedIndex = req?["proxy"]?["scheme"]?.GetValue<string>() switch { "https" => 1, "socks5" => 2, _ => 0 };
        host.Text = req?["proxy"]?["host"]?.GetValue<string>() ?? ""; user.Text = req?["proxy"]?["usr"]?.GetValue<string>() ?? ""; password.Password = req?["proxy"]?["pwd"]?.GetValue<string>() ?? "";
        foreach (var (control, key) in new[] { (autoTorrent, "autoTorrent"), (deleteTorrent, "deleteTorrentAfterDownload"), (extract, "autoExtract") }) control.SelectedIndex = initial?["opts"]?["extra"]?[key] is JsonValue value ? value.GetValue<bool>() ? 1 : 2 : 0;
        archivePassword.Password = initial?["opts"]?["extra"]?["archivePassword"]?.GetValue<string>() ?? "";
        deleteArchive.IsChecked = initial?["opts"]?["extra"]?["deleteAfterExtract"]?.GetValue<bool>() ?? config["archive"]?["deleteAfterExtract"]?.GetValue<bool>() ?? false;
    }
    public void Apply(JsonObject request)
    {
        var req = request["req"]!.AsObject(); req["extra"] ??= new JsonObject(); var extra = req["extra"]!.AsObject();
        extra["method"] = method.Text.Trim().Length == 0 ? "GET" : method.Text.Trim().ToUpperInvariant(); extra["body"] = body.Text;
        if (trackers.Text.Length > 0) extra["trackers"] = ConfigJson.Array(ConfigJson.Lines(trackers.Text));
        req["skipVerifyCert"] = skipCert.IsChecked == true;
        if (proxyMode.SelectedIndex == 2 && host.Text.Trim().Length == 0) throw new FormatException(Strings.Get("Errors.ProxyHost"));
        req["proxy"] = new JsonObject { ["mode"] = proxyMode.SelectedIndex switch { 1 => "none", 2 => "custom", _ => "follow" }, ["scheme"] = scheme.SelectedItem?.ToString() ?? "http", ["host"] = host.Text.Trim(), ["usr"] = user.Text, ["pwd"] = password.Password };
        var opts = request["opts"]!.AsObject(); opts["extra"] ??= new JsonObject();
        foreach (var (control, key) in new[] { (autoTorrent, "autoTorrent"), (deleteTorrent, "deleteTorrentAfterDownload"), (extract, "autoExtract") }) opts["extra"]![key] = control.SelectedIndex == 0 ? null : JsonValue.Create(control.SelectedIndex == 1);
        opts["extra"]!["archivePassword"] = archivePassword.Password; opts["extra"]!["deleteAfterExtract"] = deleteArchive.IsChecked == true;
    }
}
