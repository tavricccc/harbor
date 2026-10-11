using Microsoft.UI.Windowing;

namespace Harbor.Services;

internal sealed partial class ConfirmationTopmost
{
    // Confirmation windows and their pickers share the UI thread.
    private static readonly Dictionary<nint, ConfirmationTopmost> owners = [];
    private int openPickers;
    private bool IsEnforced => active && openPickers == 0;

    internal static IDisposable? SuspendForPicker(nint owner)
    {
        if (!owners.TryGetValue(owner, out var policy))
            return null; // Settings and other ordinary windows have no topmost policy.

        policy.openPickers++;
        if (policy.openPickers == 1)
        {
            if (policy.window.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = false;
            WindowActivation.SetTopmost(policy.window, false);
        }
        return new PickerScope(policy);
    }

    private sealed class PickerScope(ConfirmationTopmost policy) : IDisposable
    {
        public void Dispose()
        {
            policy.openPickers--;
            // Starting a download or closing the owner ends the confirmation policy.
            if (!policy.IsEnforced)
                return;
            if (policy.window.AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = true;
            policy.Refresh();
        }
    }
}
