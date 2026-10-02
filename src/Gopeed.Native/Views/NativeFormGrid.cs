using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Gopeed_Native.Views;

internal sealed class NativeFormGrid : Grid
{
    public NativeFormGrid(double labelWidth = 72, double rowSpacing = 14)
    {
        ColumnSpacing = 16; RowSpacing = rowSpacing;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    public void AddField(string label, FrameworkElement field)
    {
        var row = RowDefinitions.Count;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var title = new TextBlock
        {
            Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, field is TextBox or ComboBox or NumberBox or PasswordBox ? 6 : 0, 0, 0),
            Style = (Style)Application.Current.Resources["SecondaryTextBlockStyle"], FontSize = 14
        };
        Grid.SetRow(title, row); Children.Add(title);
        Grid.SetRow(field, row); Grid.SetColumn(field, 1); Children.Add(field);
    }

    public TextBlock AddText(string label, string value)
    {
        var text = new TextBlock { Text = value, FontSize = 14, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        AddField(label, text); return text;
    }
}
