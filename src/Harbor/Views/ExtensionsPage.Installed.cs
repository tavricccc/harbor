using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class ExtensionsPage
{
    private async Task ReloadInstalled()
    {
        try
        {
            installed.Children.Clear(); identities.Clear(); var data = (await vm.Core.GetAsync("extensions"))!.AsArray();
            if (data.Count == 0) { installed.Children.Add(new TextBlock { Text = "尚未安裝擴充功能。" }); return; }
            foreach (var node in data)
            {
                var ext = node!.AsObject(); var identity = ext["identity"]!.GetValue<string>(); identities.Add(identity); var route = "extensions/" + Uri.EscapeDataString(identity);
                var section = new StackPanel { Spacing = 10 };
                section.Children.Add(new TextBlock { Text = $"{ext["title"]}  {ext["version"]}", Style = (Style)Application.Current.Resources["CompactSectionTitleStyle"] });
                section.Children.Add(new TextBlock { Text = ext["description"]?.ToString() ?? "", TextWrapping = TextWrapping.Wrap });
                var enabled = new ToggleSwitch { Header = "啟用", IsOn = ext["disabled"]?.GetValue<bool>() != true };
                enabled.Toggled += async (_, _) => { enabled.IsEnabled = false; try { await vm.Core.SendAsync(HttpMethod.Put, route+"/switch", new JsonObject { ["status"] = enabled.IsOn }); } catch (Exception error) { Error(error); await ReloadInstalled(); } finally { enabled.IsEnabled = true; } }; section.Children.Add(enabled);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var settings = new Button { Content = "設定" }; settings.Click += async (_, _) => await EditSettings(ext, route); actions.Children.Add(settings);
                var update = new Button { Content = "檢查更新", IsEnabled = ext["repository"]?["url"]?.GetValue<string>() is { Length: > 0 } };
                update.Click += async (_, _) => { update.IsEnabled = false; try { var result = await vm.Core.GetAsync(route+"/update"); var version = result?["newVersion"]?.GetValue<string>(); if (string.IsNullOrEmpty(version)) { message.Severity = InfoBarSeverity.Success; message.Message = "已是最新版本。"; message.IsOpen = true; } else { var dialog = new ContentDialog { Title = $"更新 {ext["title"]}？", Content = $"{ext["version"]} → {version}", PrimaryButtonText = "更新", CloseButtonText = "取消" }; if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary) { await vm.Core.SendAsync(HttpMethod.Post, route+"/update"); await ReloadInstalled(); } } } catch (Exception error) { Error(error); } finally { update.IsEnabled = true; } }; actions.Children.Add(update);
                var remove = new Button { Content = "解除安裝" }; remove.Click += async (_, _) => { var dialog = new ContentDialog { Title = "解除安裝擴充功能？", Content = ext["title"]!.GetValue<string>(), PrimaryButtonText = "解除安裝", CloseButtonText = "取消" }; if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary) { try { await vm.Core.SendAsync(HttpMethod.Delete, route); await ReloadInstalled(); await LoadStore(true); } catch (Exception error) { Error(error); } } }; actions.Children.Add(remove);
                section.Children.Add(actions); var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 }; AddLink(links, "詳細介紹", ext["repository"]?["url"]?.GetValue<string>()); AddLink(links, "網站", ext["homepage"]?.GetValue<string>()); section.Children.Add(links); installed.Children.Add(section);
            }
        }
        catch (Exception error) { Error(error); }
    }
    private async Task EditSettings(JsonObject ext, string route)
    {
        var panel = new StackPanel { Spacing = 12 }; var readers = new Dictionary<string, Func<JsonNode?>>();
        var errorBar = new InfoBar { Severity = InfoBarSeverity.Error }; panel.Children.Add(errorBar);
        foreach (var node in ext["settings"]?.AsArray() ?? [])
        {
            var item = node!; var name = item["name"]!.GetValue<string>(); var title = item["title"]?.GetValue<string>() ?? name;
            if (item["options"] is JsonArray options && options.Count > 0)
            {
                var box = new ComboBox { Header = title, HorizontalAlignment = HorizontalAlignment.Stretch }; foreach (var option in options) box.Items.Add(new ComboBoxItem { Content = option!["label"]!.GetValue<string>(), Tag = option["value"]!.DeepClone() });
                box.SelectedIndex = Math.Max(0, options.ToList().FindIndex(o => JsonNode.DeepEquals(o?["value"],item["value"]))); panel.Children.Add(box); readers[name] = () => ((JsonNode)((ComboBoxItem)box.SelectedItem).Tag).DeepClone();
            }
            else if (item["type"]!.GetValue<string>() == "boolean") { var box = new ToggleSwitch { Header = title, IsOn = item["value"]?.GetValue<bool>() ?? false }; panel.Children.Add(box); readers[name] = () => JsonValue.Create(box.IsOn); }
            else if (item["type"]!.GetValue<string>() == "number") { var box = new NumberBox { Header = title, Value = item["value"]?.GetValue<double>() ?? 0 }; panel.Children.Add(box); readers[name] = () => double.IsFinite(box.Value) ? JsonValue.Create(box.Value) : throw new FormatException($"請輸入有效的{title}。"); }
            else { var box = new TextBox { Header = title, Text = item["value"]?.ToString() ?? "" }; panel.Children.Add(box); readers[name] = () => JsonValue.Create(box.Text); }
            if (item["description"]?.GetValue<string>() is { Length: > 0 } description) panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap });
        }
        if (readers.Count == 0) panel.Children.Add(new TextBlock { Text = "此擴充功能沒有可調整的設定。" });
        var dialog = new ContentDialog { Title = ext["title"]!.GetValue<string>(), Content = new ScrollViewer { Content = panel, MaxHeight = 400 }, PrimaryButtonText = "儲存", CloseButtonText = "取消" };
        dialog.PrimaryButtonClick += async (_, click) => { var deferral = click.GetDeferral(); try { var settings = new JsonObject(); foreach (var pair in readers) settings[pair.Key] = pair.Value(); await vm.Core.SendAsync(HttpMethod.Put, route+"/settings", new JsonObject { ["settings"] = settings }); } catch (Exception error) { click.Cancel = true; errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; } finally { deferral.Complete(); } };
        await NativeDialogs.ShowAsync(dialog, XamlRoot); await ReloadInstalled();
    }
}
