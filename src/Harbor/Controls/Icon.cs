using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;
namespace Harbor.Controls;

/// <summary>A Lucide stroke icon (24 × 24, line width 1.75, scaled with the control).</summary>
public sealed partial class Icon : UserControl
{
    // Lucide (ISC License, see Assets/LICENSE-Lucide.txt), 24 × 24 stroke paths.
    static readonly Dictionary<string, string> Paths = new()
    {
        ["close"] = "M 18 6 L 6 18 M 6 6 l 12 12",
        ["minus"] = "M 5 12 h 14",
        ["gear"] = "M 9.671 4.136 a 2.34 2.34 0 0 1 4.659 0 a 2.34 2.34 0 0 0 3.319 1.915 a 2.34 2.34 0 0 1 2.33 4.033 a 2.34 2.34 0 0 0 0 3.831 a 2.34 2.34 0 0 1 -2.33 4.033 a 2.34 2.34 0 0 0 -3.319 1.915 a 2.34 2.34 0 0 1 -4.659 0 a 2.34 2.34 0 0 0 -3.32 -1.915 a 2.34 2.34 0 0 1 -2.33 -4.033 a 2.34 2.34 0 0 0 0 -3.831 A 2.34 2.34 0 0 1 6.35 6.051 a 2.34 2.34 0 0 0 3.319 -1.915 M 9 12 A 3 3 0 1 0 15 12 A 3 3 0 1 0 9 12 Z",
        ["globe"] = "M 2 12 A 10 10 0 1 0 22 12 A 10 10 0 1 0 2 12 Z M 12 2 a 14.5 14.5 0 0 0 0 20 a 14.5 14.5 0 0 0 0 -20 M 2 12 h 20",
        ["keyboard"] = "M 10 8 h 0.01 M 12 12 h 0.01 M 14 8 h 0.01 M 16 12 h 0.01 M 18 8 h 0.01 M 6 8 h 0.01 M 7 16 h 10 M 8 12 h 0.01 M 4 4 H 20 A 2 2 0 0 1 22 6 V 18 A 2 2 0 0 1 20 20 H 4 A 2 2 0 0 1 2 18 V 6 A 2 2 0 0 1 4 4 Z",
        ["case"] = "M 2 16 l 4.039 -9.69 a 0.5 0.5 0 0 1 0.923 0 L 11 16 M 22 9 v 7 M 3.304 13 h 6.392 M 15 12.5 A 3.5 3.5 0 1 0 22 12.5 A 3.5 3.5 0 1 0 15 12.5 Z",
        ["pencil"] = "M 21.174 6.812 a 1 1 0 0 0 -3.986 -3.987 L 3.842 16.174 a 2 2 0 0 0 -0.5 0.83 l -1.321 4.352 a 0.5 0.5 0 0 0 0.623 0.622 l 4.353 -1.32 a 2 2 0 0 0 0.83 -0.497 z M 15 5 l 4 4",
        ["list"] = "M 3 5 h 0.01 M 3 12 h 0.01 M 3 19 h 0.01 M 8 5 h 13 M 8 12 h 13 M 8 19 h 13",
        ["palette"] = "M 11 17 a 4 4 0 0 1 -8 0 V 5 a 2 2 0 0 1 2 -2 h 4 a 2 2 0 0 1 2 2 Z M 16.7 13 H 19 a 2 2 0 0 1 2 2 v 4 a 2 2 0 0 1 -2 2 H 7 M 7 17 h 0.01 M 11 8 l 2.3 -2.3 a 2.4 2.4 0 0 1 3.404 0.004 L 18.6 7.6 a 2.4 2.4 0 0 1 0.026 3.434 L 9.9 19.8",
        ["bookmark"] = "M 17 3 a 2 2 0 0 1 2 2 v 15 a 1 1 0 0 1 -1.496 0.868 l -4.512 -2.578 a 2 2 0 0 0 -1.984 0 l -4.512 2.578 A 1 1 0 0 1 5 20 V 5 a 2 2 0 0 1 2 -2 z",
        ["info"] = "M 2 12 A 10 10 0 1 0 22 12 A 10 10 0 1 0 2 12 Z M 12 16 v -4 M 12 8 h 0.01",
        ["check"] = "M 20 6 L 9 17 l -5 -5",
        ["warn"] = "M 21.73 18 l -8 -14 a 2 2 0 0 0 -3.48 0 l -8 14 A 2 2 0 0 0 4 21 h 16 a 2 2 0 0 0 1.73 -3 M 12 9 v 4 M 12 17 h 0.01",
        ["chev-d"] = "M 6 9 l 6 6 l 6 -6",
        ["shield"] = "M 20 13 c 0 5 -3.5 7.5 -7.66 8.95 a 1 1 0 0 1 -0.67 -0.01 C 7.5 20.5 4 18 4 13 V 6 a 1 1 0 0 1 1 -1 c 2 0 4.5 -1.2 6.24 -2.72 a 1.17 1.17 0 0 1 1.52 0 C 14.51 3.81 17 5 19 5 a 1 1 0 0 1 1 1 z M 9 12 l 2 2 l 4 -4",
        ["trash"] = "M 10 11 v 6 M 14 11 v 6 M 19 6 v 14 a 2 2 0 0 1 -2 2 H 7 a 2 2 0 0 1 -2 -2 V 6 M 3 6 h 18 M 8 6 V 4 a 2 2 0 0 1 2 -2 h 4 a 2 2 0 0 1 2 2 v 2",
        ["plus"] = "M 5 12 h 14 M 12 5 v 14",
        ["clipboard"] = "M 9 2 H 15 A 1 1 0 0 1 16 3 V 5 A 1 1 0 0 1 15 6 H 9 A 1 1 0 0 1 8 5 V 3 A 1 1 0 0 1 9 2 Z M 16 4 h 2 a 2 2 0 0 1 2 2 v 14 a 2 2 0 0 1 -2 2 H 6 a 2 2 0 0 1 -2 -2 V 6 a 2 2 0 0 1 2 -2 h 2",
        ["play"] = "M 6 3 L 20 12 L 6 21 Z",
        ["pause"] = "M 15 4 H 17 A 1 1 0 0 1 18 5 V 19 A 1 1 0 0 1 17 20 H 15 A 1 1 0 0 1 14 19 V 5 A 1 1 0 0 1 15 4 Z M 7 4 H 9 A 1 1 0 0 1 10 5 V 19 A 1 1 0 0 1 9 20 H 7 A 1 1 0 0 1 6 19 V 5 A 1 1 0 0 1 7 4 Z",
        ["retry"] = "M 21 12 a 9 9 0 1 1 -9 -9 c 2.52 0 4.93 1 6.74 2.74 L 21 8 M 21 3 v 5 h -5",
        ["folder"] = "M 20 20 a 2 2 0 0 0 2 -2 V 8 a 2 2 0 0 0 -2 -2 h -7.9 a 2 2 0 0 1 -1.69 -.9 L 9.6 3.9 A 2 2 0 0 0 7.93 3 H 4 a 2 2 0 0 0 -2 2 v 13 a 2 2 0 0 0 2 2 Z",
        ["search"] = "M 3 11 A 8 8 0 1 0 19 11 A 8 8 0 1 0 3 11 Z M 21 21 l -4.3 -4.3",
        ["more"] = "M 11 12 A 1 1 0 1 0 13 12 A 1 1 0 1 0 11 12 Z M 18 12 A 1 1 0 1 0 20 12 A 1 1 0 1 0 18 12 Z M 4 12 A 1 1 0 1 0 6 12 A 1 1 0 1 0 4 12 Z",
        ["sort"] = "M 21 16 l -4 4 -4 -4 M 17 20 V 4 M 3 8 l 4 -4 4 4 M 7 4 v 16",
        ["download"] = "M 12 15 V 3 M 21 15 v 4 a 2 2 0 0 1 -2 2 H 5 a 2 2 0 0 1 -2 -2 v -4 M 7 10 l 5 5 5 -5",
        ["open"] = "M 15 3 h 6 v 6 M 10 14 21 3 M 18 13 v 6 a 2 2 0 0 1 -2 2 H 5 a 2 2 0 0 1 -2 -2 V 8 a 2 2 0 0 1 2 -2 h 6",
        ["file"] = "M 15 2 H 6 a 2 2 0 0 0 -2 2 v 16 a 2 2 0 0 0 2 2 h 12 a 2 2 0 0 0 2 -2 V 7 Z M 14 2 v 4 a 2 2 0 0 0 2 2 h 4",
        ["archive"] = "M 3 3 H 21 A 1 1 0 0 1 22 4 V 7 A 1 1 0 0 1 21 8 H 3 A 1 1 0 0 1 2 7 V 4 A 1 1 0 0 1 3 3 Z M 4 8 v 11 a 2 2 0 0 0 2 2 h 12 a 2 2 0 0 0 2 -2 V 8 M 10 12 h 4",
        ["app"] = "M 4 4 H 20 A 2 2 0 0 1 22 6 V 18 A 2 2 0 0 1 20 20 H 4 A 2 2 0 0 1 2 18 V 6 A 2 2 0 0 1 4 4 Z M 10 4 v 4 M 2 8 h 20 M 6 4 v 4",
        ["music"] = "M 9 18 V 5 l 12 -2 v 13 M 3 18 A 3 3 0 1 0 9 18 A 3 3 0 1 0 3 18 Z M 15 16 A 3 3 0 1 0 21 16 A 3 3 0 1 0 15 16 Z",
        ["video"] = "M 16 13 l 5.223 3.482 a .5 .5 0 0 0 .777 -.416 V 7.87 a .5 .5 0 0 0 -.752 -.432 L 16 10.5 M 4 6 H 14 A 2 2 0 0 1 16 8 V 16 A 2 2 0 0 1 14 18 H 4 A 2 2 0 0 1 2 16 V 8 A 2 2 0 0 1 4 6 Z",
        ["image"] = "M 5 3 H 19 A 2 2 0 0 1 21 5 V 19 A 2 2 0 0 1 19 21 H 5 A 2 2 0 0 1 3 19 V 5 A 2 2 0 0 1 5 3 Z M 7 9 A 2 2 0 1 0 11 9 A 2 2 0 1 0 7 9 Z M 21 15 l -3.086 -3.086 a 2 2 0 0 0 -2.828 0 L 6 21",
        ["back"] = "M 12 19 l -7 -7 7 -7 M 19 12 H 5",
        ["chev-r"] = "M 9 18 l 6 -6 -6 -6",
        ["chev-u"] = "M 18 15 l -6 -6 -6 6",
        ["link"] = "M 10 13 a 5 5 0 0 0 7.54 .54 l 3 -3 a 5 5 0 0 0 -7.07 -7.07 l -1.72 1.71 M 14 11 a 5 5 0 0 0 -7.54 -.54 l -3 3 a 5 5 0 0 0 7.07 7.07 l 1.71 -1.71",
        ["clock"] = "M 2 12 A 10 10 0 1 0 22 12 A 10 10 0 1 0 2 12 Z M 12 6 v 6 l 4 2",
        ["magnet"] = "M 6 15 l -4 -4 6.75 -6.77 a 7.79 7.79 0 0 1 11 11 L 13 22 l -4 -4 6.39 -6.36 a 2.14 2.14 0 0 0 -3 -3 L 6 15 M 5 8 l 4 4 M 12 15 l 4 4",
    };
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(Icon), new PropertyMetadata(null, (d, _) => ((Icon)d).Update()));
    public string? Glyph { get => (string?)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    readonly Path path = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, StrokeThickness = 1.75 };

    public Icon()
    {
        IsTabStop = false;
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(path);
        Content = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
        // Follows the inherited foreground (a button's presenter, hover states) unless one is set here. A binding to the
        // inherited value misses a window theme switch, so the stroke is read again on every change that can move it.
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => Stroke());
        ActualThemeChanged += (_, _) => Stroke();
        Loaded += (_, _) => Stroke();
    }

    void Stroke() => path.Stroke = Foreground;

    // A Geometry cannot be shared between elements; each icon parses its own.
    void Update() => path.Data = Glyph is { } g && Paths.TryGetValue(g, out var data) ? (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), data) : null;
}
