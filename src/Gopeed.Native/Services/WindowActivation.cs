using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace Gopeed_Native.Services;

internal static class WindowActivation
{
    // Transfer the launcher's foreground permission before redirecting activation.
    internal static void AllowRedirect(uint processId) => AllowSetForegroundWindow(processId);

    internal static void ShowConfirmation(Window window)
    {
        window.AppWindow.Show(true);
        window.Activate();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        // Apply Z-order after WinUI shows the HWND. Keep confirmation above the
        // browser even when Windows declines keyboard-focus transfer.
        SetWindowPos(hwnd, new nint(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
}
