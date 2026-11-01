using Microsoft.UI.Xaml.Media;

namespace Harbor.Services;

/// <summary>
/// Harbor's font: Noto Sans when it is installed, otherwise the Windows UI font for the language. XAML skips the
/// families in a list that are not installed, so the list itself is the lookup.
/// </summary>
internal static class UiFont
{
    internal static FontFamily For(string language) => new(language.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) || language.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)
        ? "Noto Sans SC, Noto Sans CJK SC, Microsoft YaHei UI"
        : language.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "Noto Sans TC, Noto Sans CJK TC, Noto Sans HK, Microsoft JhengHei UI"
            : "Noto Sans, Noto Sans TC, Segoe UI Variable Text, Microsoft JhengHei UI");
}
