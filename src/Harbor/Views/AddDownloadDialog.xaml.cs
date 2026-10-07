using Microsoft.UI.Xaml.Controls;
using Harbor.Services;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed partial class AddDownloadDialog : ContentDialog
{
    private readonly DownloadForm form;
    public AddDownloadDialog(CoreClient core, JsonObject? initial = null)
    {
        InitializeComponent();
        form = new DownloadForm(core, initial) { MaxHeight = 500 };
        Content = form;
        form.StateChanged += () => { PrimaryButtonText = form.ActionText; IsPrimaryButtonEnabled = !form.IsBusy; };
    }
    private async void Submit(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        var deferral = args.GetDeferral();
        bool complete;
        try { complete = await form.SubmitAsync(); }
        finally { deferral.Complete(); }
        if (complete) Hide();
    }
}
