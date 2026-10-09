using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json.Nodes;
using Harbor.Services;
using Windows.Storage.Pickers;

namespace Harbor.Views;

public sealed record DownloadCategory(string Name, string Path)
{
    public override string ToString() => $"{Name} · {Path}";
}

internal sealed class CategoriesEditor : StackPanel
{
    private readonly ListView list = new() { MaxHeight = 220, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox name = new();
    private readonly TextBox path = new();
    private readonly InfoBar message = new() { Severity = InfoBarSeverity.Error, IsClosable = true };
    private readonly CheckBox enabled = new() { Content = Strings.Get("Categories.SaveByCategory") };

    public CategoriesEditor(nint windowHandle)
    {
        Spacing = 20;
        Children.Add(enabled);
        NativeInfoBars.CollapseWhenClosed(message);
        AutomationProperties.SetName(name, Strings.Get("Categories.Name"));
        AutomationProperties.SetName(path, Strings.Get("Common.SaveLocation"));
        list.ItemContainerStyle = new Style { TargetType = typeof(ListViewItem), Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } };
        list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="4,8" ColumnSpacing="16">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="104"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Name}" FontSize="14" TextTrimming="CharacterEllipsis"/>
                    <TextBlock Grid.Column="1" Text="{Binding Path}" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                </Grid>
            </DataTemplate>
            """);
        var table = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        list.Items.VectorChanged += (_, _) => table.Visibility = list.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var heading = new Grid { Padding = new Thickness(16, 0, 16, 0), ColumnSpacing = 16 };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var location = SettingsFields.Description(Strings.Get("Common.SaveLocation"));
        heading.Children.Add(SettingsFields.Description(Strings.Get("Common.Category")));
        Grid.SetColumn(location, 1);
        heading.Children.Add(location);
        table.Children.Add(heading);
        table.Children.Add(list);
        Children.Add(table);
        Children.Add(message);

        var form = new NativeFormGrid();
        form.AddField(Strings.Get("Common.Name"), name);
        var folderRow = new Grid { ColumnSpacing = 8 };
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        folderRow.Children.Add(path);
        var browse = NativeButtons.Create(Strings.Get("Common.Browse"), "\uE8B7");
        browse.Click += async (_, _) =>
        {
            try
            {
                var picker = new FolderPicker();
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
                var folder = await picker.PickSingleFolderAsync();
                if (folder is not null)
                    path.Text = folder.Path;
            }
            catch (Exception error) { message.Message = UserError.Message(error); message.IsOpen = true; }
        };
        Grid.SetColumn(browse, 1);
        folderRow.Children.Add(browse);
        form.AddField(Strings.Get("Common.SaveTo"), folderRow);
        Children.Add(form);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = NativeButtons.Create(Strings.Get("Categories.Add"), "\uE710");
        var remove = NativeButtons.Create(Strings.Get("Categories.Remove"), "\uE74D");
        remove.IsEnabled = false;
        var clear = NativeButtons.Create(Strings.Get("Common.ClearSelection"), "\uE711");
        clear.IsEnabled = false;
        list.SelectionChanged += (_, _) =>
        {
            var selected = list.SelectedItem as DownloadCategory;
            name.Text = selected?.Name ?? "";
            path.Text = selected?.Path ?? "";
            remove.IsEnabled = clear.IsEnabled = selected is not null;
            NativeButtons.SetContent(add, selected is null ? Strings.Get("Categories.Add") : Strings.Get("Categories.Update"), selected is null ? "\uE710" : "\uE74E");
        };
        add.Click += (_, _) =>
        {
            if (name.Text.Trim().Length == 0 || !System.IO.Path.IsPathFullyQualified(path.Text))
            {
                message.Message = Strings.Get("Errors.Category");
                message.IsOpen = true;
                return;
            }
            var item = new DownloadCategory(name.Text.Trim(), path.Text.Trim());
            var selected = list.SelectedIndex;
            if (selected >= 0)
                list.Items[selected] = item;
            else
                list.Items.Add(item);
            list.SelectedIndex = -1;
            name.Text = "";
            path.Text = "";
            message.IsOpen = false;
        };
        remove.Click += (_, _) => { if (list.SelectedIndex >= 0) list.Items.RemoveAt(list.SelectedIndex); };
        clear.Click += (_, _) => list.SelectedIndex = -1;
        actions.Children.Add(add);
        actions.Children.Add(remove);
        actions.Children.Add(clear);
        Children.Add(actions);

        var presets = NativeButtons.Create(Strings.Get("Categories.AddPresets"), "\uE8B7");
        presets.Click += (_, _) =>
        {
            var root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            foreach (var label in new[] { Strings.Get("Categories.Music"), Strings.Get("Categories.Video"), Strings.Get("Categories.Document"), Strings.Get("Categories.Program"), Strings.Get("Categories.Archive"), Strings.Get("Categories.Other") })
                if (!list.Items.Cast<DownloadCategory>().Any(x => x.Name == label))
                    list.Items.Add(new DownloadCategory(label, System.IO.Path.Combine(root, label)));
        };
        Children.Add(presets);
    }

    public void Load(JsonObject config)
    {
        enabled.IsChecked = UsesCategories(config);
        list.Items.Clear();
        foreach (var item in Read(config))
            list.Items.Add(item);
    }
    public void Save(JsonObject config)
    {
        ConfigJson.Set(config, "extra.harborCategorizedDownloads", JsonValue.Create(enabled.IsChecked == true));
        ConfigJson.Set(config, "extra.downloadCategories", new JsonArray(list.Items.Cast<DownloadCategory>().Select(x => (JsonNode?)new JsonObject { ["name"] = x.Name, ["path"] = x.Path, ["isBuiltIn"] = false, ["isDeleted"] = false }).ToArray()));
    }
    public static bool UsesCategories(JsonNode config) => ConfigJson.Get(config, "extra.harborCategorizedDownloads")?.GetValue<bool>() == true;
    public static IReadOnlyList<DownloadCategory> Read(JsonNode config) => (ConfigJson.Get(config, "extra.downloadCategories")?.AsArray() ?? []).Where(x => x?["isDeleted"]?.GetValue<bool>() != true).Select(x => new DownloadCategory(x!["name"]?.GetValue<string>() is { Length: > 0 } title ? title : x["nameKey"]?.GetValue<string>() switch { "categoryMusic" => Strings.Get("Categories.Music"), "categoryVideo" => Strings.Get("Categories.Video"), "categoryDocument" => Strings.Get("Categories.Document"), "categoryProgram" => Strings.Get("Categories.Program"), "categoryArchive" => Strings.Get("Categories.Archive"), _ => Strings.Get("Categories.Other") }, x["path"]!.GetValue<string>())).ToList();
}
