using Microsoft.Windows.ApplicationModel.DynamicDependency;
using System.Runtime.CompilerServices;

namespace Harbor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--check-dotnet"))
            return 0;
        if (Services.IntegrationCommands.TryRun(args))
            return 0;

        // Initialize the shared runtime before loading WinUI. Bootstrap's native
        // dialog offers the matching runtime download when it is missing.
        if (!Bootstrap.TryInitialize(Microsoft.WindowsAppSDK.Release.MajorMinor,
            Microsoft.WindowsAppSDK.Release.VersionTag,
            new PackageVersion(Microsoft.WindowsAppSDK.Runtime.Version.UInt64),
            args.Contains("--check-runtime") ? Bootstrap.InitializeOptions.None : Bootstrap.InitializeOptions.OnNoMatch_ShowUI, out var error))
            return error;
        try { return args.Contains("--check-runtime") ? 0 : RunApplication(); }
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
