using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;

namespace Harbor.Services;

// Owns the topmost policy for the confirmation phase, including native frame
// changes made by WinUI while content loads, expands or opens an owned picker.
internal sealed class ConfirmationTopmost : IDisposable
{
    private readonly Window window;
    private readonly nint hwnd;
    private readonly Subclass callback;
    private bool active = true;
    private const nuint SubclassId = 0x48415242;

    internal ConfirmationTopmost(Window window)
    {
        this.window = window;
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        callback = WindowProcedure;
        if (!SetWindowSubclass(hwnd, callback, SubclassId, 0))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        window.Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (active && window.AppWindow.IsVisible)
            WindowActivation.KeepAbove(window);
    }

    internal void Refresh()
    {
        if (active)
            WindowActivation.KeepAbove(window);
    }

    private nint WindowProcedure(nint handle, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        if (active && message == 0x0046) // WM_WINDOWPOSCHANGING
        {
            var position = Marshal.PtrToStructure<WindowPosition>(lParam);
            if ((position.Flags & 0x0004) == 0) // A native Z-order change.
            {
                position.InsertAfter = new nint(-1);
                Marshal.StructureToPtr(position, lParam, false);
            }
        }
        return DefSubclassProc(handle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (!active)
            return;
        active = false;
        window.Activated -= OnActivated;
        RemoveWindowSubclass(hwnd, callback, SubclassId);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPosition
    {
        public nint Handle, InsertAfter;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint Subclass(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint data);
    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(nint hwnd, Subclass callback, nuint id, nuint data);
    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(nint hwnd, Subclass callback, nuint id);
    [DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);
}
