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
    private readonly TextBox headers = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 220, PlaceholderText = "每行一組，例如 Referer: https://example.com" };
    private readonly CheckBox resume = new() { Content = "更新後繼續下載", IsChecked = true };
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

        Title = "修改下載來源";
        PrimaryButtonText = "更新來源";
        CloseButtonText = "取消";
        DefaultButton = ContentDialogButton.Primary;
        AutomationProperties.SetName(source, "來源網址");
        AutomationProperties.SetName(headers, "HTTP 標頭");
        var form = new NativeFormGrid(labelWidth: 80, rowSpacing: 20);
        form.AddField("來源網址", source);
        form.AddField("HTTP 標頭", headers);
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
                throw new FormatException("請輸入有效的 HTTP 或 HTTPS 來源網址。");
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
