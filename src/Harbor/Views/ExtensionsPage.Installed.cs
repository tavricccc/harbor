using Harbor.Controls;
using Harbor.Localization;
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
            installed.Children.Clear();
            identities.Clear();
            var data = (await vm.Core.GetAsync("extensions"))!.AsArray();
            SetInstalledStatus(data.Count == 0 ? (Strings.Get("Extensions.NoneInstalled"), false) : null);
            foreach (var node in data)
                installed.Children.Add(InstalledCard(node!.AsObject()));
        }
        catch (Exception error) { SetInstalledStatus((UserError.Message(error), true)); }
    }

    private Border InstalledCard(JsonObject ext)
    {
        var identity = ext["identity"]!.GetValue<string>();
        identities.Add(identity);
        var route = "extensions/" + Uri.EscapeDataString(identity);
        var enabled = new Switch { IsOn = ext["disabled"]?.GetValue<bool>() != true, VerticalAlignment = VerticalAlignment.Top };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(enabled, Strings.Get("Common.Enable"));
        enabled.RegisterPropertyChangedCallback(Switch.IsOnProperty, async (_, _) =>
        {
            enabled.IsEnabled = false;
            try { await vm.Core.SendAsync(HttpMethod.Put, route + "/switch", new JsonObject { ["status"] = enabled.IsOn }); }
            catch (Exception error) { Error(error); await ReloadInstalled(); }
            finally { enabled.IsEnabled = true; }
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        var settings = new Button { Content = Strings.Get("Common.Settings") };
        settings.Click += async (_, _) => await EditSettings(ext, route);
        actions.Children.Add(settings);
        var update = new Button { Content = Strings.Get("Update.Check"), IsEnabled = ext["repository"]?["url"]?.GetValue<string>() is { Length: > 0 } };
        update.Click += async (_, _) => await CheckUpdate(ext, route, update);
        actions.Children.Add(update);
        var remove = new Button { Content = Strings.Get("Common.Uninstall") };
        remove.Click += async (_, _) => await Uninstall(ext, route);
        actions.Children.Add(remove);
        var links = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        AddLink(links, Strings.Get("Extensions.Details"), ext["repository"]?["url"]?.GetValue<string>());
        AddLink(links, Strings.Get("Common.Website"), ext["homepage"]?.GetValue<string>());
        return Card(ext["title"]?.ToString() ?? identity, ext["version"]?.ToString() ?? "", ext["description"]?.ToString() ?? "", null, enabled, actions, links);
    }

    private async Task CheckUpdate(JsonObject ext, string route, Button update)
    {
        update.IsEnabled = false;
        try
        {
            var result = await vm.Core.GetAsync(route + "/update");
            var version = result?["newVersion"]?.GetValue<string>();
            if (string.IsNullOrEmpty(version)) { Success(Strings.Get("Update.UpToDate")); return; }
            var dialog = new ContentDialog { Title = Strings.Format("Extensions.UpdateConfirm", ext["title"]), Content = $"{ext["version"]} → {version}", PrimaryButtonText = Strings.Get("Common.Update"), CloseButtonText = Strings.Get("Common.Cancel"), DefaultButton = ContentDialogButton.Primary };
            if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary)
            {
                await vm.Core.SendAsync(HttpMethod.Post, route + "/update");
                await ReloadInstalled();
            }
        }
        catch (Exception error) { Error(error); }
        finally { update.IsEnabled = true; }
    }

    private async Task Uninstall(JsonObject ext, string route)
    {
        var body = new StackPanel { Spacing = 8, Children =
        {
            new TextBlock { Text = ext["title"]!.GetValue<string>(), Style = (Style)Application.Current.Resources["CompactSectionTitleStyle"] },
            new TextBlock { Text = Strings.Get("Extensions.UninstallNote"), Style = (Style)Application.Current.Resources["UiBody"], TextWrapping = TextWrapping.Wrap },
        } };
        var dialog = new ContentDialog { Title = Strings.Get("Extensions.UninstallConfirm"), Content = body, PrimaryButtonText = Strings.Get("Common.Uninstall"), CloseButtonText = Strings.Get("Common.Cancel") };
        if (await NativeDialogs.ShowAsync(dialog, XamlRoot) != ContentDialogResult.Primary) return;
        try { await vm.Core.SendAsync(HttpMethod.Delete, route); await ReloadInstalled(); await LoadStore(true); }
        catch (Exception error) { Error(error); }
    }
    private async Task EditSettings(JsonObject ext, string route)
    {
        var panel = new StackPanel { Spacing = 12 };
        var readers = new Dictionary<string, Func<JsonNode?>>();
        var errorBar = new InfoBar { Severity = InfoBarSeverity.Error };
        panel.Children.Add(errorBar);
        foreach (var node in ext["settings"]?.AsArray() ?? [])
        {
            var item = node!;
            var name = item["name"]!.GetValue<string>();
            var title = item["title"]?.GetValue<string>() ?? name;
            if (item["options"] is JsonArray options && options.Count > 0)
            {
                var box = new ComboBox { Header = title, HorizontalAlignment = HorizontalAlignment.Stretch };
                foreach (var option in options)
                    box.Items.Add(new ComboBoxItem { Content = option!["label"]!.GetValue<string>(), Tag = option["value"]!.DeepClone() });
                box.SelectedIndex = Math.Max(0, options.ToList().FindIndex(o => JsonNode.DeepEquals(o?["value"], item["value"])));
                panel.Children.Add(box);
                readers[name] = () => ((JsonNode)((ComboBoxItem)box.SelectedItem).Tag).DeepClone();
            }
            else if (item["type"]!.GetValue<string>() == "boolean")
            {
                var box = new ToggleSwitch { Header = title, IsOn = item["value"]?.GetValue<bool>() ?? false };
                panel.Children.Add(box);
                readers[name] = () => JsonValue.Create(box.IsOn);
            }
            else if (item["type"]!.GetValue<string>() == "number")
            {
                var box = new NumberBox { Header = title, Value = item["value"]?.GetValue<double>() ?? 0 };
                panel.Children.Add(box);
                readers[name] = () => double.IsFinite(box.Value) ? JsonValue.Create(box.Value) : throw new FormatException(Strings.Format("Errors.InvalidField", title));
            }
            else
            {
                var box = new TextBox { Header = title, Text = item["value"]?.ToString() ?? "" };
                panel.Children.Add(box);
                readers[name] = () => JsonValue.Create(box.Text);
            }
            if (item["description"]?.GetValue<string>() is { Length: > 0 } description)
                panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap });
        }
        if (readers.Count == 0)
            panel.Children.Add(new TextBlock { Text = Strings.Get("Extensions.NoSettings") });
        var dialog = new ContentDialog { Title = ext["title"]!.GetValue<string>(), Content = new ScrollViewer { Content = panel, MaxHeight = 400 }, PrimaryButtonText = Strings.Get("Common.Save"), CloseButtonText = Strings.Get("Common.Cancel") };
        dialog.PrimaryButtonClick += async (_, click) => { var deferral = click.GetDeferral(); try { var settings = new JsonObject(); foreach (var pair in readers) settings[pair.Key] = pair.Value(); await vm.Core.SendAsync(HttpMethod.Put, route + "/settings", new JsonObject { ["settings"] = settings }); } catch (Exception error) { click.Cancel = true; errorBar.Message = UserError.Message(error); errorBar.IsOpen = true; } finally { deferral.Complete(); } };
        await NativeDialogs.ShowAsync(dialog, XamlRoot);
        await ReloadInstalled();
    }
}
