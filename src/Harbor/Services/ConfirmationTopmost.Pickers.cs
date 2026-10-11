using Microsoft.UI.Windowing;

namespace Harbor.Services;

internal sealed partial class ConfirmationTopmost
{
    // Confirmation windows and their pickers share the UI thread.
    private static readonly Dictionary<nint, ConfirmationTopmost> owners = [];
    private static int openPickers;
    internal static bool HasOpenPicker => openPickers > 0;
    private bool IsEnforced => active && openPickers == 0;

    internal static IDisposable SuspendForPicker(nint owner)
    {
        openPickers++;
        // Keep the native owner topmost so its modal dialog inherits that band.
        // Pause forced Z-order changes across all confirmations while it is open.
        return new PickerScope(owner);
    }

    private void SetTopmost(bool topmost)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = topmost;
        WindowActivation.SetTopmost(window, topmost);
    }

    private sealed class PickerScope(nint owner) : IDisposable
    {
        public void Dispose()
        {
            openPickers--;
            if (HasOpenPicker)
                return;
            // Disposed confirmations have left the registry; restore only live ones.
            foreach (var policy in owners.Values)
                policy.SetTopmost(true);
            if (owners.TryGetValue(owner, out var originatingWindow))
                originatingWindow.Refresh();
        }
    }
}
