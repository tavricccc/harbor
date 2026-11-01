using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System.Runtime.CompilerServices;

namespace HarborSetup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The glass window needs Windows 11's backdrop and corner APIs; stop before anything is installed.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            MessageBoxW(0, "Harbor 需要 Windows 11 21H2（組建 22000）以上。\nHarbor requires Windows 11 21H2 (build 22000) or later.", "Harbor", 0x10); // MB_ICONERROR
            return 1;
        }
        // Bootstrap's native dialog offers the matching Windows App Runtime download
        // before any WinUI type loads; the apphost already reported missing .NET.
        if (!Bootstrap.TryInitialize(Microsoft.WindowsAppSDK.Release.MajorMinor,
            Microsoft.WindowsAppSDK.Release.VersionTag,
            new PackageVersion(Microsoft.WindowsAppSDK.Runtime.Version.UInt64),
            Bootstrap.InitializeOptions.OnNoMatch_ShowUI, out var error))
            return error;
        try { return RunApplication(); }
        finally { Bootstrap.Shutdown(); }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplication()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Microsoft.UI.Xaml.Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }
}
