using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace Harbor.Services;

public static class ShareFiles
{
    private static readonly Dictionary<nint, (DataTransferManager Manager, TypedEventHandler<DataTransferManager, DataRequestedEventArgs> Handler)> pending = [];
    public static void Show(nint window, string path)
    {
        Release(window);
        var manager = DataTransferManagerInterop.GetForWindow(window);
        TypedEventHandler<DataTransferManager, DataRequestedEventArgs> handler = async (_, args) =>
        {
            var deferral = args.Request.GetDeferral();
            try
            {
                args.Request.Data.Properties.Title = Path.GetFileName(path);
                args.Request.Data.SetStorageItems([await StorageFile.GetFileFromPathAsync(path)]);
            }
            catch (Exception error) { args.Request.FailWithDisplayText(UserError.Message(error)); }
            finally { deferral.Complete(); Release(window); }
        };
        pending[window] = (manager, handler);
        manager.DataRequested += handler;
        DataTransferManagerInterop.ShowShareUIForWindow(window);
    }
    public static void Release(nint window)
    {
        if (pending.Remove(window, out var state))
            state.Manager.DataRequested -= state.Handler;
    }
}
