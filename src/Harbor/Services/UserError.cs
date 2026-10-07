using System.Net.Http;

namespace Harbor.Services;

public sealed class DownloadApiException(string message) : Exception(message);
public static class UserError
{
    public static string Message(Exception error)
    {
        if (error is DownloadApiException)
        {
            var text = error.Message.ToLowerInvariant();
            if (text.Contains("header")) return "HTTP 標頭格式不正確，請檢查請求選項。";
            if (text.Contains("not found")) return "找不到下載來源或紀錄，請檢查連結後重試。";
            if (text.Contains("permission") || text.Contains("access is denied")) return "無法寫入檔案，請確認儲存位置與檔案是否正被使用。";
            if (text.Contains("unsupported") || text.Contains("invalid url")) return "無法辨識這個下載連結，請檢查格式。";
            return "操作未完成，請檢查下載來源或稍後重試。";
        }
        if (error is HttpRequestException or TaskCanceledException) return "目前無法連線，請稍後重試。";
        if (error is UnauthorizedAccessException) return "無法存取檔案，請選擇其他儲存位置。";
        if (error is FormatException) return error.Message;
        if (error.Message.Any(c => c is >= '\u4e00' and <= '\u9fff')) return error.Message;
        return "操作未完成，請稍後重試。";
    }
}
