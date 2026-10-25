using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System.Runtime.CompilerServices;

namespace HarborSetup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
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
