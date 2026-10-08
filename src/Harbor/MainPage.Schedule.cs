using Harbor.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Text.Json.Nodes;
using Harbor.Services;
using Harbor.Views;

namespace Harbor;

public sealed partial class MainPage
{
    public async void ShowDeferredDownloads()
    {
        await ready.Task;
        ShowDownloads(this, new RoutedEventArgs());
        SearchBox.Text = "";
        FilterBox.SelectedIndex = 2;
        await ViewModel.RefreshAsync();
    }
    private async void EditSchedule(object sender, RoutedEventArgs args)
    {
        if (ContextItem(sender) is not { IsDeferred: true } item) return;
        var initial = (item.ScheduledAt ?? DateTimeOffset.Now.AddHours(1)).ToLocalTime();
        var date = new CalendarDatePicker { Header = Strings.Get("Common.Date"), Date = initial, MinDate = DateTimeOffset.Now.Date };
        var time = new TimePicker { Header = Strings.Get("Common.Time"), Time = initial.TimeOfDay, ClockIdentifier = "24HourClock" };
        var error = new InfoBar { Severity = InfoBarSeverity.Error };
        NativeInfoBars.CollapseWhenClosed(error);
        var dialog = new ContentDialog {
            Title = Strings.Get("Schedule.EditTitle"), PrimaryButtonText = Strings.Get("Schedule.Save"), SecondaryButtonText = Strings.Get("Schedule.MakeLater"), CloseButtonText = Strings.Get("Common.Cancel"),
            Content = new StackPanel { Spacing = 12, Children = { error, new TextBlock { Text = item.Name, TextWrapping = TextWrapping.Wrap }, date, time,
                SettingsFields.Description(Strings.Get("Schedule.Description")) } }
        };
        async Task Save(ContentDialogButtonClickEventArgs click, bool schedule)
        {
            var deferral = click.GetDeferral();
            try {
                if (schedule && date.Date is null) throw new FormatException(Strings.Get("Errors.ScheduleDate"));
                var startAt = schedule ? new DateTimeOffset(DateTime.SpecifyKind(date.Date!.Value.Date + time.Time, DateTimeKind.Local)) : (DateTimeOffset?)null;
                await ViewModel.Core.SendAsync(HttpMethod.Patch, "native/queue/" + item.Id, new JsonObject { ["startAt"] = startAt?.ToString("O") });
            }
            catch (Exception failure) { click.Cancel = true; error.Message = UserError.Message(failure); error.IsOpen = true; }
            finally { deferral.Complete(); }
        }
        dialog.PrimaryButtonClick += async (_, click) => await Save(click, true);
        dialog.SecondaryButtonClick += async (_, click) => await Save(click, false);
        await NativeDialogs.ShowAsync(dialog, XamlRoot);
        await ViewModel.RefreshAsync();
    }
}
