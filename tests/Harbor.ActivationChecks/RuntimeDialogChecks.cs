using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

internal static class RuntimeDialogChecks
{
    internal static async Task Run(string application, string missingDotNetApplication)
    {
        var native = NativeLibrary.Load(Path.Combine(application, "Microsoft.WindowsAppRuntime.Bootstrap.dll"));
        var initialize = Marshal.GetDelegateForFunctionPointer<Initialize>(NativeLibrary.GetExport(native, "MddBootstrapInitialize2"));
        var assembly = Assembly.LoadFrom(Path.Combine(application, "Harbor.dll"));
        var release = assembly.GetType("Microsoft.WindowsAppSDK.Release")!;
        var version = (uint)release.GetField("MajorMinor", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        var failedInitialization = Task.Run(() => initialize(version, "", 99UL << 48, 8)); // OnNoMatch_ShowUI
        var runtimeDialog = await FindDialog(Environment.ProcessId);
        Decline(runtimeDialog);
        if (await failedInitialization.WaitAsync(TimeSpan.FromSeconds(10)) >= 0)
            throw new Exception("Missing Windows App Runtime unexpectedly initialized");
        NativeLibrary.Free(native);
        Console.WriteLine("PASS: missing Windows App Runtime shows an installation dialog; declining returns failure.");

        var emptyRoot = Path.Combine(Path.GetTempPath(), "Harbor-MissingDotNet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        var start = new ProcessStartInfo(Path.Combine(missingDotNetApplication, "Harbor.exe")) { UseShellExecute = false, CreateNoWindow = true };
        start.Environment["DOTNET_ROOT_X64"] = emptyRoot;
        start.Environment["DOTNET_ROOT"] = emptyRoot;
        start.Environment.Remove("DOTNET_DISABLE_GUI_ERRORS");
        start.ArgumentList.Add("--check-dotnet");
        using var child = Process.Start(start)!;
        try
        {
            var dotNetDialog = await FindDialog(child.Id);
            Console.WriteLine(".NET installation prompt found.");
            PostMessage(dotNetDialog, 0x0010, 0, 0); // WM_CLOSE; decline the download.
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            if (child.ExitCode == 0) throw new Exception("Missing .NET unexpectedly started");
            Console.WriteLine("PASS: missing .NET shows the host installation dialog; declining exits without starting Harbor.");
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            Directory.Delete(emptyRoot);
        }
    }

    private static async Task<nint> FindDialog(int process)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            nint dialog = 0;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var owner);
                if (owner == process && IsWindowVisible(window))
                    dialog = window;
                return true;
            }, 0);
            if (dialog != 0) return dialog;
            await Task.Delay(100);
        }
        throw new Exception("Installation prompt did not appear");
    }

    private static void Decline(nint dialog)
    {
        var title = new StringBuilder(256);
        GetWindowText(dialog, title, title.Capacity);
        Console.WriteLine("Installation prompt: " + title);
        PostMessage(dialog, 0x0111, 7, 0); // WM_COMMAND / IDNO. Never opens a browser or installs anything.
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Initialize(uint version, [MarshalAs(UnmanagedType.LPWStr)] string tag, ulong minimum, uint options);
    private delegate bool EnumWindow(nint window, nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint GetDlgItem(nint window, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
