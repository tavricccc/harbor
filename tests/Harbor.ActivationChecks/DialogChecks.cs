using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Automation;

internal static class DialogChecks
{
    internal static async Task Run(nint hwnd, string profile)
    {
        var owner = AutomationElement.FromHandle(hwnd);
        await Invoke(owner, "Choose folder");
        nint dialog = 0;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (dialog == 0 && DateTime.UtcNow < deadline)
        {
            EnumWindows((candidate, _) =>
            {
                if (GetWindow(candidate, 4) == hwnd && IsWindowVisible(candidate))
                    dialog = candidate;
                return true;
            }, 0);
            await Task.Delay(100);
        }
        if (dialog == 0) throw new Exception("Browse did not open an owned folder dialog");
        if ((GetWindowLongPtr(dialog, -20).ToInt64() & 8) == 0)
            throw new Exception("Folder dialog is hidden behind the topmost confirmation");
        // Dismiss the system dialog through its standard cancel message; the
        // application's async handler must return and re-enable Browse.
        PostMessage(dialog, 0x0111, 2, 0); // WM_COMMAND / IDCANCEL
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (IsWindow(dialog) && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (IsWindow(dialog)) throw new Exception("Folder dialog did not cancel");
        var browse = Find(owner, "Choose folder");
        if (!browse.Current.IsEnabled) throw new Exception("Browse stayed disabled after cancelling");
        if ((GetWindowLongPtr(hwnd, -20).ToInt64() & 8) == 0)
            throw new Exception("Cancelling Browse removed confirmation topmost state");
        Console.WriteLine("PASS: Browse opens an owned topmost dialog; cancellation restores the enabled button and retains confirmation topmost.");
        await Invoke(owner, "Choose folder");
        deadline = DateTime.UtcNow.AddSeconds(10);
        dialog = 0;
        while (dialog == 0 && DateTime.UtcNow < deadline)
        {
            EnumWindows((candidate, _) =>
            {
                if (GetWindow(candidate, 4) == hwnd && IsWindowVisible(candidate)) dialog = candidate;
                return true;
            }, 0);
            await Task.Delay(100);
        }
        if (dialog == 0) throw new Exception("Second Browse invocation failed");
        await Invoke(AutomationElement.FromHandle(dialog), "Choose folder");
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (IsWindow(dialog) && DateTime.UtcNow < deadline) await Task.Delay(100);
        var destination = owner.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "DownloadDestination"));
        var selected = ((ValuePattern)destination.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
        if (!string.Equals(selected, Path.Combine(profile, "picked-folder"), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Browse returned wrong folder: " + selected);
        Console.WriteLine("PASS: Browse returns the selected folder to the download form.");
        await Invoke(owner, "Start download");
        deadline = DateTime.UtcNow.AddSeconds(10);
        while ((GetWindowLongPtr(hwnd, -20).ToInt64() & 8) != 0 && DateTime.UtcNow < deadline) await Task.Delay(100);
        if ((GetWindowLongPtr(hwnd, -20).ToInt64() & 8) != 0)
            throw new Exception("Starting download did not release topmost state");
        Console.WriteLine("PASS: starting a download releases confirmation topmost state.");
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (!Find(owner, "Cancel").Current.IsEnabled && DateTime.UtcNow < deadline) await Task.Delay(100);
        await Invoke(owner, "Cancel");
        deadline = DateTime.UtcNow.AddSeconds(10);
        while (IsWindow(hwnd) && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (IsWindow(hwnd)) throw new Exception("Cancel did not close the confirmation");
        Console.WriteLine("PASS: cancelling the progress window destroys it.");
    }

    internal static async Task CancelConfirmation(nint hwnd)
    {
        await Invoke(AutomationElement.FromHandle(hwnd), "Cancel");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (IsWindow(hwnd) && DateTime.UtcNow < deadline) await Task.Delay(100);
        if (IsWindow(hwnd)) throw new Exception("Cancel did not close the pending confirmation");
        Console.WriteLine("PASS: cancelling a pending confirmation destroys its topmost window.");
    }

    private static AutomationElement Find(AutomationElement owner, string name) =>
        owner.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, name)))
        ?? throw new Exception("Button not found: " + name);

    private static Task Invoke(AutomationElement owner, string name) => Task.Run(() =>
        ((InvokePattern)Find(owner, name).GetCurrentPattern(InvokePattern.Pattern)).Invoke());

    private delegate bool Callback(nint hwnd, nint data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, nint data);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);
}
