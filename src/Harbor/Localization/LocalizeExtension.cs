using Microsoft.UI.Xaml.Markup;

namespace Harbor.Localization;

[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class LocalizeExtension : MarkupExtension
{
    public string Key { get; set; } = "";
    protected override object ProvideValue() => Strings.Get(Key);
}
