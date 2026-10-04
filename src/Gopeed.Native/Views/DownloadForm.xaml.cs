using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gopeed_Native.Services;
using Gopeed_Native.Models;
using System.Net.Http;
using System.Text.Json.Nodes;
using Windows.Storage.Pickers;

namespace Gopeed_Native.Views;

public sealed partial class DownloadForm : UserControl
{
 private readonly CoreClient core;
 private string? resolved;
 private readonly JsonObject? initial;
 private readonly nint owner;
 private string lastInput = "";
 private readonly DownloadOptionsPanel requestOptions = new();
 public string? CreatedTaskId { get; private set; }
 public string ActionText { get; private set; } = "檢查連結";
 public bool IsBusy { get; private set; }
 public event Action? StateChanged;
 public event Action? LayoutChanged;
 public double MeasureContentHeight(double width) { FormContent.Measure(new Windows.Foundation.Size(width, double.PositiveInfinity)); return FormContent.DesiredSize.Height; }
 public DownloadForm(CoreClient core, JsonObject? initial = null, nint? owner = null, bool compact = false)
 {
  this.core = core; this.initial = initial; this.owner = owner ?? App.WindowHandle; InitializeComponent();
  OptionsSurface.Children.Add(requestOptions);
  requestOptions.RequestChanged += InvalidateResolution;
  Headers.TextChanged += (_, _) => InvalidateResolution();
  FormContent.SizeChanged += (_, _) => LayoutChanged?.Invoke();
  if (compact) { Links.AcceptsReturn = false; Links.TextWrapping = TextWrapping.NoWrap; Links.MinHeight = 32; Links.MaxHeight = double.PositiveInfinity; ManualActions.Visibility = Visibility.Collapsed; }
  Loaded += InitializeForm;
 }
 private async void InitializeForm(object sender, RoutedEventArgs e)
 {
  Loaded -= InitializeForm; SetBusy(true);
  try
  {
   var config = (await core.GetAsync("config"))!; Destination.Text = config["downloadDir"]?.GetValue<string>() ?? "";
   requestOptions.Load(initial, config); Connections.Value = config["protocolConfig"]?["http"]?["connections"]?.GetValue<int>() ?? 8;
   DirectDownload.IsChecked = config["extra"]?["defaultDirectDownload"]?.GetValue<bool>() == true;
   foreach (var category in CategoriesEditor.Read(config)) Category.Items.Add(new ComboBoxItem { Content = category.Name, Tag = category.Path });
   CategoryLabel.Visibility = Category.Visibility = Category.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
   var prefs = UiPreferences.Load();
   if (prefs.RememberDownloadDirectory && prefs.LastDownloadDirectory.Length > 0) Destination.Text = prefs.LastDownloadDirectory;
   if (Destination.Text.Length == 0) Destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
   ApplyInitial();
   var links = Links.Text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
   if (initial is not null && links.Length == 1 && DirectDownload.IsChecked != true) await InspectAsync(links[0]);
   else if (links.Length > 1) SetAction($"開始 {links.Length} 個下載");
  }
  catch (Exception error) { ShowError(error); }
  finally { SetBusy(false); if (ConfigJson.Lines(Links.Text).Length == 0) Links.Focus(FocusState.Programmatic); else FileName.Focus(FocusState.Programmatic); }
 }
 private void ApplyInitial()
 {
  if (initial is null) return;
  Links.Text = initial["req"]?["url"]?.GetValue<string>() ?? "";
  lastInput = Links.Text;
  if (initial["opts"]?["path"]?.GetValue<string>() is { Length: > 0 } path) Destination.Text = path;
  FileName.Text = initial["opts"]?["name"]?.GetValue<string>() ?? "";
  if (initial["opts"]?["extra"]?["connections"] is JsonValue connections) Connections.Value = connections.GetValue<int>();
  if (initial["req"]?["extra"]?["header"] is JsonObject headers) Headers.Text = string.Join("\n", headers.Select(pair => $"{pair.Key}: {pair.Value}"));
 }
 public Button DetachOptionsButton()
 {
  ManualActions.Children.Remove(AdvancedButton);
  return AdvancedButton;
 }
 private void InputChanged(object s, TextChangedEventArgs e) { if (Files is null || lastInput == Links.Text) return; lastInput = Links.Text; InvalidateResolution(); }
 private void InvalidateResolution()
 {
  resolved = null;
  var links = ConfigJson.Lines(Links.Text);
  SetAction(links.Length > 1 ? $"開始 {links.Length} 個下載" : DirectDownload.IsChecked == true ? "開始下載" : "檢查連結");
  KindLabel.Text = links.Length > 1 ? "批次" : links.FirstOrDefault() is { } link && DownloadSources.IsTorrent(link) ? "BT" : "檔案";
  Files.Visibility = Visibility.Collapsed;
  FileSelectionActions.Visibility = Visibility.Collapsed;
  FilesSurface.Visibility = Visibility.Collapsed;
  Preview.Text = DirectDownload.IsChecked == true ? "開始下載後才會知道" : "檢查連結後顯示";
 }
 private void DirectChanged(object sender, RoutedEventArgs e) { if (Files is not null) InvalidateResolution(); }
 private void CategoryChanged(object sender, SelectionChangedEventArgs e) { if (Category.SelectedItem is ComboBoxItem item) Destination.Text = (string)item.Tag; }
 private async void PickFolder(object s, RoutedEventArgs e)
 {
  var picker = new FolderPicker(); picker.FileTypeFilter.Add("*"); WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
  var folder = await picker.PickSingleFolderAsync(); if (folder is not null) Destination.Text = folder.Path;
 }
 private async void PickTorrent(object s, RoutedEventArgs e)
 {
  var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".torrent"); WinRT.Interop.InitializeWithWindow.Initialize(picker, owner);
  var files = await picker.PickMultipleFilesAsync(); if (files.Count > 0) Links.Text = string.Join("\n", files.Select(file => file.Path));
 }
 private JsonObject BuildRequest(string url)
 {
  var headers = HttpHeaders.Parse(Headers.Text);
  var request = initial?.DeepClone().AsObject() ?? new JsonObject();
  var req = request["req"] as JsonObject ?? new JsonObject();
  var requestExtra = req["extra"] as JsonObject ?? new JsonObject();
  var opts = request["opts"] as JsonObject ?? new JsonObject();
  var optionExtra = opts["extra"] as JsonObject ?? new JsonObject();
  requestExtra["header"] = headers; req["url"] = url;
  if (req["extra"] is null) req["extra"] = requestExtra;
  optionExtra["connections"] = (int)Connections.Value; opts["path"] = Destination.Text.Trim(); opts["name"] = FileName.Text.Trim();
  if (opts["extra"] is null) opts["extra"] = optionExtra;
  if (request["req"] is null) request["req"] = req;
  if (request["opts"] is null) request["opts"] = opts;
  requestOptions.Apply(request);
  return request;
 }
 public async Task<bool> SubmitAsync(bool defer = false, DateTimeOffset? startAt = null)
 {
  bool complete = false;
  SetBusy(true); Message.IsOpen = false; Message.Visibility = Visibility.Collapsed;
  try
  {
   var links = Links.Text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
   if (links.Length == 0) throw new FormatException("請輸入下載連結。");
   if (double.IsNaN(Connections.Value)) throw new FormatException("請輸入連線數。");
   if (!Path.IsPathFullyQualified(Destination.Text.Trim())) throw new FormatException("請選擇完整的儲存路徑。");
   if (FileName.Text.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new FormatException("檔名含有無法使用的字元。");
   Directory.CreateDirectory(Destination.Text.Trim());
   if (defer)
   {
    if (links.Length > 1 && FileName.Text.Length > 0) throw new FormatException("批次下載請留空檔名，避免檔案名稱重複。");
    var requests = new JsonArray(links.Select(link => (JsonNode?)BuildRequest(link)).ToArray());
    if (links.Length == 1 && resolved is not null && DirectDownload.IsChecked != true)
    {
     if (Files.Items.Count > 1 && Files.SelectedItems.Count == 0) throw new FormatException("請至少選擇一個檔案。");
     requests[0]!["opts"]!["selectFiles"] = new JsonArray(Files.SelectedItems.Cast<ResolvedFile>().Select(file => (JsonNode?)JsonValue.Create(file.Index)).ToArray());
    }
    await core.SendAsync(HttpMethod.Post, "native/queue", new JsonObject { ["reqs"] = requests, ["startAt"] = startAt?.ToString("O") });
    complete = true;
   }
   else if (links.Length > 1)
   {
    if (FileName.Text.Length > 0) throw new FormatException("批次下載請留空檔名，避免檔案名稱重複。");
    var requests = new JsonArray(links.Select(link => (JsonNode?)BuildRequest(link)).ToArray());
    await core.SendAsync(HttpMethod.Post, "tasks/batch", new JsonObject { ["reqs"] = requests }); complete = true;
   }
   else if (resolved is null && DirectDownload.IsChecked != true)
   {
    await InspectAsync(links[0]);
   }
   else
   {
    // Update resolution options if destination, file name or file selection changed after probing.
    var request = BuildRequest(links[0]);
    if (DirectDownload.IsChecked != true) { request["opts"]!["selectFiles"] = new JsonArray(Files.SelectedItems.Cast<ResolvedFile>().Select(file => JsonValue.Create(file.Index) as JsonNode).ToArray()); if (Files.Items.Count > 1 && Files.SelectedItems.Count == 0) throw new FormatException("請至少選擇一個檔案。"); }
    CreatedTaskId = (await core.SendAsync(HttpMethod.Post, "tasks", request))!.GetValue<string>(); complete = true;
   }
  }
  catch (Exception e) { ShowError(e); }
  finally { SetBusy(false); }
  if (complete) { var prefs = UiPreferences.Load(); if (prefs.RememberDownloadDirectory) prefs.LastDownloadDirectory = Destination.Text.Trim(); prefs.RecentLinks = linksForHistory().Concat(prefs.RecentLinks).Distinct().Take(30).ToList(); prefs.Save(); }
  return complete;
 }
 private async Task InspectAsync(string url)
 {
  Busy.Visibility = Visibility.Visible; Busy.IsActive = true;
  try
  {
   var result = (await core.SendAsync(HttpMethod.Post, "resolve", BuildRequest(url)))!;
   resolved = result["id"]!.GetValue<string>(); var resource = result["res"]!;
   var displayName = resource["name"]?.GetValue<string>();
   if (string.IsNullOrEmpty(displayName)) displayName = resource["files"]!.AsArray()[0]!["name"]!.GetValue<string>();
   var size = resource["size"]!.GetValue<long>();
   Preview.Text = size > 0 ? DownloadItem.FormatBytes(size) : "由來源於下載時提供";
   Files.Items.Clear(); var index = 0;
   foreach (var file in resource["files"]!.AsArray()) Files.Items.Add(new ResolvedFile(index++, Path.Combine(file!["path"]?.GetValue<string>() ?? "", file["name"]!.GetValue<string>()), file["size"]!.GetValue<long>()));
   Files.SelectAll(); Files.Visibility = Files.Items.Count > 1 ? Visibility.Visible : Visibility.Collapsed; FileSelectionActions.Visibility = FilesSurface.Visibility = Files.Visibility;
   if (initial?["opts"]?["selectFiles"] is JsonArray selected && selected.Count > 0) { var indexes = selected.Select(x => x!.GetValue<int>()).ToHashSet(); foreach (var file in Files.SelectedItems.Cast<ResolvedFile>().ToList()) if (!indexes.Contains(file.Index)) Files.SelectedItems.Remove(file); }
   if (initial is not null && Files.Items.Count == 1 && FileName.Text.Length == 0) FileName.Text = displayName;
   SetAction("開始下載");
  }
  finally { Busy.IsActive = false; Busy.Visibility = Visibility.Collapsed; }
 }
 private IEnumerable<string> linksForHistory() => ConfigJson.Lines(Links.Text).Where(x => !x.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
 private void SelectAllFiles(object sender, RoutedEventArgs e) => Files.SelectAll();
 private void SelectNoFiles(object sender, RoutedEventArgs e) => Files.SelectedItems.Clear();
 private async void ShowRecent(object sender, RoutedEventArgs e)
 {
  var history = new ListView { ItemsSource = UiPreferences.Load().RecentLinks, MaxHeight = 340, SelectionMode = ListViewSelectionMode.Single };
  var clear = new Button { Content = "清除最近使用的連結" }; clear.Click += (_, _) => { var prefs = UiPreferences.Load(); prefs.RecentLinks.Clear(); prefs.Save(); history.ItemsSource = prefs.RecentLinks; };
  var dialog = new ContentDialog { Title = "最近使用的連結", Content = new StackPanel { Spacing = 12, Children = { history, clear } }, PrimaryButtonText = "使用連結", CloseButtonText = "取消", IsPrimaryButtonEnabled = false };
  history.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = history.SelectedItem is not null;
  if (await NativeDialogs.ShowAsync(dialog, XamlRoot) == ContentDialogResult.Primary) Links.Text = (string)history.SelectedItem;
 }
 private void ShowError(Exception e) { Message.Message = UserError.Message(e); Message.Visibility = Visibility.Visible; Message.IsOpen = true; LayoutChanged?.Invoke(); }
 private void SetAction(string text) { ActionText = text; StateChanged?.Invoke(); LayoutChanged?.Invoke(); }
 private void SetBusy(bool value) { IsBusy = value; Busy.IsActive = value; Busy.Visibility = value ? Visibility.Visible : Visibility.Collapsed; StateChanged?.Invoke(); }
 private sealed record ResolvedFile(int Index, string Name, long Size) { public override string ToString() => $"{Name} · {DownloadItem.FormatBytes(Size)}"; }
}
