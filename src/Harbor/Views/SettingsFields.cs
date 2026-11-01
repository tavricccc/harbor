using Harbor.Controls;
using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.Text.Json.Nodes;
using Harbor.Services;

namespace Harbor.Views;

/// <summary>
/// Settings bound to core config paths, each added as a settings row (label left, control right; multi-line text under
/// its label) to a <see cref="SettingCard"/> made by <see cref="Group"/>.
/// </summary>
internal sealed class SettingsFields
{
    private const double FieldWidth = 280, NumberWidth = 140;
    private readonly List<(Action<JsonObject> Load, Action<JsonObject> Save)> bindings = [];
    public void Load(JsonObject config)
    {
        foreach (var field in bindings)
            field.Load(config);
    }
    public void Save(JsonObject config)
    {
        foreach (var field in bindings)
            field.Save(config);
    }
    public TextBox Text(Panel card, string title, string path, bool lines = false, string initial = "", bool array = true)
    {
        var box = new TextBox { AcceptsReturn = lines, TextWrapping = lines ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = lines ? 88 : 0, MaxHeight = lines ? 200 : double.PositiveInfinity };
        if (!lines)
            box.Width = FieldWidth;
        AutomationProperties.SetAutomationId(box, "Setting-" + path);
        AutomationProperties.SetName(box, title);
        Row(card, title, box, stacked: lines);
        bindings.Add((c => box.Text = lines && array ? string.Join("\n", ConfigJson.Get(c, path)?.AsArray().Select(x => x!.GetValue<string>()) ?? []) : ConfigJson.Get(c, path)?.GetValue<string>() ?? initial,
            c => ConfigJson.Set(c, path, lines && array ? ConfigJson.Array(ConfigJson.Lines(box.Text)) : JsonValue.Create(box.Text.Trim()))));
        return box;
    }
    /// <summary>Binds a text box the page laid out itself to a string path.</summary>
    public void Bind(TextBox box, string path) =>
        bindings.Add((c => box.Text = ConfigJson.Get(c, path)?.GetValue<string>() ?? "", c => ConfigJson.Set(c, path, JsonValue.Create(box.Text.Trim()))));
    public PasswordBox Password(Panel card, string title, string path)
    {
        var box = new PasswordBox { Width = FieldWidth };
        AutomationProperties.SetAutomationId(box, "Setting-" + path);
        AutomationProperties.SetName(box, title);
        Row(card, title, box);
        bindings.Add((c => box.Password = ConfigJson.Get(c, path)?.GetValue<string>() ?? "", c => ConfigJson.Set(c, path, JsonValue.Create(box.Password))));
        return box;
    }
    public NumberBox Number(Panel card, string title, string path, double initial, double min, double max, double scale = 1, bool fractional = false)
    {
        var box = new NumberBox { Width = NumberWidth, MinWidth = 0, Minimum = min, Maximum = max, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        AutomationProperties.SetAutomationId(box, "Setting-" + path);
        AutomationProperties.SetName(box, title);
        Row(card, title, box);
        bindings.Add((c => box.Value = (ConfigJson.Get(c, path)?.GetValue<double>() ?? initial * scale) / scale, c =>
        {
            if (!double.IsFinite(box.Value) || box.Value < min || box.Value > max)
                throw new FormatException(Strings.Format("Errors.InvalidSetting", title));
            ConfigJson.Set(c, path, fractional ? JsonValue.Create(box.Value * scale) : JsonValue.Create((long)(box.Value * scale)));
        }
        ));
        return box;
    }
    public SettingToggle Toggle(Panel card, string title, string path, bool initial = false)
    {
        var toggle = new SettingToggle { Label = title };
        AutomationProperties.SetAutomationId(toggle, "Setting-" + path);
        card.Children.Add(toggle);
        bindings.Add((c => toggle.IsOn = ConfigJson.Get(c, path)?.GetValue<bool>() ?? initial, c => ConfigJson.Set(c, path, JsonValue.Create(toggle.IsOn))));
        return toggle;
    }

    /// <summary>A group title (optional), its card and a note under it (optional).</summary>
    public static SettingCard Group(Panel page, string? title = null, string? note = null)
    {
        if (title is not null)
            page.Children.Add(new TextBlock { Text = title, Style = (Style)Application.Current.Resources["UiGroupTitle"] });
        var card = new SettingCard();
        page.Children.Add(card);
        if (note is not null)
            page.Children.Add(Note(note));
        return card;
    }

    public static SettingRow Row(Panel card, string title, UIElement control, string? description = null, bool stacked = false)
    {
        var row = new SettingRow { Label = title, Description = description ?? "", Control = control, Stacked = stacked };
        card.Children.Add(row);
        return row;
    }

    /// <summary>Content that is not a labelled field (an editor, a guide), padded like a row.</summary>
    public static Border Block(Panel card, UIElement content)
    {
        var block = new Border { Padding = new Thickness(14, 12, 14, 14), Child = content };
        card.Children.Add(block);
        return block;
    }

    public static void Reveal(SettingToggle toggle, params UIElement[] content)
    {
        void Update()
        {
            foreach (var element in content)
                element.Visibility = toggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        }
        toggle.RegisterPropertyChangedCallback(SettingToggle.IsOnProperty, (_, _) => Update());
        Update();
    }

    public static TextBlock Note(string text) => new() { Text = text, Style = (Style)Application.Current.Resources["UiNote"] };

    public static TextBlock Description(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources["SecondaryTextBlockStyle"]
    };
}
