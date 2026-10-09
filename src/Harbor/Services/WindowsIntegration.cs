namespace Harbor.Services;

public static partial class WindowsIntegration
{
    public static void Unregister()
    {
        RemoveBrowserHost();
        RemoveFileTypes();
        RemoveStartup();
    }
}
