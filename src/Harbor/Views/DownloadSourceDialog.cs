using Harbor.Localization;
using Harbor.Models;
using Harbor.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace Harbor.Views;

public sealed class DownloadSourceDialog : ContentDialog
{
    private readonly CoreClient core;
    private readonly DownloadItem item;
    private readonly JsonObject request;
    private readonly TextBox source;
    private readonly TextBox headers = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 220, PlaceholderText = Strings.Get("Source.HeadersPlaceholder") };
    private readonly CheckBox resume = new() { Content = Strings.Get("Source.ResumeAfterUpdate"), IsChecked = true };
    private readonly InfoBar error = new() { Severity = InfoBarSeverity.Error, IsClosable = true };

    public DownloadSourceDialog(CoreClient core, DownloadItem item)
    {
        NativeInfoBars.CollapseWhenClosed(error);
        this.core = core;
        this.item = item;
        request = item.Data["meta"]!["req"]!.DeepClone().AsObject();
        source = new TextBox { Text = item.Url, TextWrapping = TextWrapping.Wrap, MaxHeight = 100 };
        if (request["extra"]?["header"] is JsonObject values)
            headers.Text = string.Join("\n", values.Select(x => $"{x.Key}: {x.Value}"));

        Title = Strings.Get("Source.Edit");
        PrimaryButtonText = Strings.Get("Source.Update");
        CloseButtonText = Strings.Get("Common.Cancel");
        DefaultButton = ContentDialogButton.Primary;
        AutomationProperties.SetName(source, Strings.Get("Common.SourceUrl"));
        AutomationProperties.SetName(headers, Strings.Get("Common.HttpHeaders"));
        var form = new NativeFormGrid(labelWidth: 80, rowSpacing: 20);
        form.AddField(Strings.Get("Common.SourceUrl"), source);
        form.AddField(Strings.Get("Common.HttpHeaders"), headers);
        Content = new StackPanel
        {
            Spacing = 20, MinWidth = 460,
            Children =
            {
                error,
                new TextBlock { Text = item.Name, TextWrapping = TextWrapping.Wrap },
                form,
                resume
            }
        };
        PrimaryButtonClick += UpdateSource;
    }

    private async void UpdateSource(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (!Uri.TryCreate(source.Text.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
                throw new FormatException(Strings.Get("Errors.SourceUrl"));
            request["url"] = url.AbsoluteUri;
            request["extra"] ??= new JsonObject();
            request["extra"]!["header"] = HttpHeaders.Parse(headers.Text);
            await core.SendAsync(HttpMethod.Patch, "tasks/" + item.Id, new JsonObject { ["req"] = request.DeepClone() });
            if (resume.IsChecked == true) await core.SendAsync(HttpMethod.Put, "tasks/" + item.Id + "/continue");
        }
        catch (Exception exception)
        {
            args.Cancel = true;
            error.Message = UserError.Message(exception);
            error.IsOpen = true;
        }
        finally { deferral.Complete(); }
    }
}
