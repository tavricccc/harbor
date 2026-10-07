using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace Harbor.Services;

public static class FileActions
{
    public static void Open(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("檔案已移動或刪除，可從儲存資料夾確認位置。", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    public static void Reveal(string path, string folder)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        if (File.Exists(path) || Directory.Exists(path)) start.Arguments = $"/select,\"{path}\"";
        else start.ArgumentList.Add(folder);
        Process.Start(start);
    }
    public static void Copy(string text)
    {
        var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data);
    }
}
