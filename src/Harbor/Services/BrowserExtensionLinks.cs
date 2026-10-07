namespace Harbor.Services;

internal static class BrowserExtensionLinks
{
    // Official store links published by GopeedLab/browser-extension.
    internal static readonly (string Browser, string Url)[] Stores =
    [
        ("Chrome", "https://chromewebstore.google.com/detail/gopeed/mijpgljlfcapndmchhjffkpckknofcnd"),
        ("Edge", "https://microsoftedge.microsoft.com/addons/detail/dkajnckekendchdleoaenoophcobooce"),
        ("Firefox", "https://addons.mozilla.org/firefox/addon/gopeed-extension")
    ];
    internal const string Documentation = "https://github.com/GopeedLab/browser-extension";
}
