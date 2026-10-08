using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Views;

internal sealed class MirrorsEditor : StackPanel
{
    private readonly CheckBox enabled = new() { Content = Strings.Get("Mirrors.Enable") };
    private readonly ListView list = new() { MaxHeight = 184, SelectionMode = ListViewSelectionMode.Single, Visibility = Visibility.Collapsed };
    private readonly ComboBox type = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Items = { "GitHub Proxy", "jsDelivr" }, SelectedIndex = 0 };
    private readonly TextBox url = new() { PlaceholderText = "https://…" };
    private readonly InfoBar message = new() { Severity = InfoBarSeverity.Error, IsClosable = true };
    private sealed record Mirror(string Type, string Url)
    {
        public string DisplayType => Type == "ghProxy" ? "GitHub Proxy" : "jsDelivr";
        public override string ToString() => $"{DisplayType} · {Url}";
    }

    public MirrorsEditor()
    {
        Spacing = 16;
        NativeInfoBars.CollapseWhenClosed(message);
        list.Items.VectorChanged += (_, _) => list.Visibility = list.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(type, Strings.Get("Mirrors.Type")); AutomationProperties.SetName(url, Strings.Get("Mirrors.Url"));
        list.ItemContainerStyle = new Style { TargetType = typeof(ListViewItem), Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } };
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="4,8" ColumnSpacing="16">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="104"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding DisplayType}" FontSize="14" TextTrimming="CharacterEllipsis"/>
                    <TextBlock Grid.Column="1" Text="{Binding Url}" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                </Grid>
            </DataTemplate>
            """);
        Children.Add(enabled);
        Children.Add(SettingsFields.Description(Strings.Get("Mirrors.Description")));
        Children.Add(list); Children.Add(message);
        var form = new NativeFormGrid(); form.AddField(Strings.Get("Common.Type"), type); form.AddField(Strings.Get("Common.Url"), url); Children.Add(form);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = NativeButtons.Create(Strings.Get("Mirrors.Add"), "\uE710");
        add.Click += (_, _) =>
        {
            if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            { message.Message = Strings.Get("Errors.MirrorUrl"); message.IsOpen = true; return; }
            list.Items.Add(new Mirror(type.SelectedIndex == 0 ? "ghProxy" : "jsdelivr", uri.AbsoluteUri.TrimEnd('/')));
            url.Text = ""; message.IsOpen = false;
        };
        var first = NativeButtons.Create(Strings.Get("Mirrors.MakeFirst"), "\uE74A"); first.IsEnabled = false;
        first.Click += (_, _) =>
        {
            var index = list.SelectedIndex;
            if (index > 0) { var value = list.Items[index]; list.Items.RemoveAt(index); list.Items.Insert(0, value); list.SelectedIndex = 0; }
        };
        var remove = NativeButtons.Create(Strings.Get("Common.Remove"), "\uE74D"); remove.IsEnabled = false;
        remove.Click += (_, _) => { if (list.SelectedIndex >= 0) list.Items.RemoveAt(list.SelectedIndex); };
        list.SelectionChanged += (_, _) => { first.IsEnabled = list.SelectedIndex > 0; remove.IsEnabled = list.SelectedIndex >= 0; };
        actions.Children.Add(add); actions.Children.Add(first); actions.Children.Add(remove); Children.Add(actions);
    }

    public void Load(JsonObject config)
    {
        enabled.IsChecked = config["extra"]?["githubMirror"]?["enabled"]?.GetValue<bool>() == true;
        list.Items.Clear();
        foreach (var mirror in config["extra"]?["githubMirror"]?["mirrors"]?.AsArray() ?? [])
            if (mirror?["isDeleted"]?.GetValue<bool>() != true) list.Items.Add(new Mirror(mirror!["type"]!.GetValue<string>(), mirror["url"]!.GetValue<string>()));
    }

    public void Save(JsonObject config)
    {
        if (enabled.IsChecked == true && list.Items.Count == 0) throw new FormatException(Strings.Get("Errors.MirrorRequired"));
        ConfigJson.Set(config, "extra.githubMirror", new JsonObject
        {
            ["enabled"] = enabled.IsChecked == true,
            ["mirrors"] = new JsonArray(list.Items.Cast<Mirror>().Select(x => (JsonNode?)new JsonObject { ["type"] = x.Type, ["url"] = x.Url, ["isBuiltIn"] = false, ["isDeleted"] = false }).ToArray())
        });
    }
}
