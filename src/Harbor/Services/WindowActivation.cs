using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace Harbor.Services;

internal static class WindowActivation
{
    // Transfer the launcher's foreground permission before redirecting activation.
    internal static void AllowRedirect(uint processId) => AllowSetForegroundWindow(processId);

    internal static void ShowConfirmation(Window window)
    {
        window.AppWindow.Show(true);
        KeepAbove(window);
        window.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        // Activation and Z-order are separate operations. A refused foreground
        // activation must not prevent the confirmation from becoming topmost.
        var activated = SetForegroundWindow(hwnd);
        KeepAbove(window);
        if (!activated)
        {
            Directory.CreateDirectory(CoreClient.DataDirectory);
            File.AppendAllText(Path.Combine(CoreClient.DataDirectory, "activation.log"),
                $"{DateTimeOffset.Now:O} foreground-denied hwnd={hwnd} foreground={GetForegroundWindow()}\n");
        }
    }

    internal static void KeepAbove(Window window) => SetWindowPos(
        WinRT.Interop.WindowNative.GetWindowHandle(window), new nint(-1), 0, 0, 0, 0,
        0x0001 | 0x0002 | 0x0010); // NOSIZE | NOMOVE | NOACTIVATE

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
