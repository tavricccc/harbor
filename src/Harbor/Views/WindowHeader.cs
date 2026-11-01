using Harbor.Controls;
using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Harbor.Views;

/// <summary>A glass window's title bar: the title on the left, minimize and close icon buttons on the right.
/// The window drags from it (<see cref="Glass.GlassWindow.SetCaption"/>).</summary>
internal sealed partial class WindowHeader : Grid
{
    private readonly TextBlock title = new() { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
    private readonly Button minimize = IconButton("minus", "Common.Minimize");
    public event Action? MinimizeRequested;
    public event Action? CloseRequested;
    public string Title { set => title.Text = value; }
    public bool ShowMinimize { set => minimize.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }

    public WindowHeader()
    {
        Height = 44;
        Padding = new Thickness(20, 0, 8, 0);
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        title.Foreground = Motion.Resource("PaneText1Brush");
        Children.Add(title);
        var close = IconButton("close", "Common.Close");
        minimize.Click += (_, _) => MinimizeRequested?.Invoke();
        close.Click += (_, _) => CloseRequested?.Invoke();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { minimize, close } };
        SetColumn(buttons, 1);
        Children.Add(buttons);
    }

    internal static Button IconButton(string glyph, string label)
    {
        var button = new Button { Style = (Style)Application.Current.Resources["PaneIconButton"], Content = new Icon { Glyph = glyph, Width = 16, Height = 16 } };
        AutomationProperties.SetName(button, Strings.Get(label));
        ToolTipService.SetToolTip(button, Strings.Get(label));
        return button;
    }
}
