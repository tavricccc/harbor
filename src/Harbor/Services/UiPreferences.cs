using System.Text.Json;

namespace Harbor.Services;

public sealed class UiPreferences
{
    public string Language { get; set; } = "";
    public bool RememberDownloadDirectory { get; set; } = true;
    public string LastDownloadDirectory { get; set; } = "";
    public bool CloseProgressAfterOpen { get; set; } = true;
    public int ApiPort { get; set; } = 18762;
    public bool CheckForUpdates { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public List<string> RecentLinks { get; set; } = [];
    public string PendingUpdateTaskId { get; set; } = "";
    public bool KeepFilesOnRemove { get; set; } = true;
    public bool BrowserSetupHintDismissed
    {
        get; set;
    }
    private static string FilePath => Path.Combine(CoreClient.DataDirectory, "preferences.json");
    public static UiPreferences Load() => File.Exists(FilePath)
        ? JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(FilePath))!
        : new();
    public void Save()
    {
        Directory.CreateDirectory(CoreClient.DataDirectory);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}
