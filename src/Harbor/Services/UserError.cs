using Harbor.Localization;
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
            var key = text switch
            {
                "download link is required" => "Errors.QueueLink",
                "invalid download link" => "Errors.QueueInvalidLink",
                "schedule must be in the future" => "Errors.FutureTime",
                "invalid schedule data" => "Errors.InvalidSchedule",
                _ when text.StartsWith("pending download not found:") => "Errors.DeferredNotFound",
                _ => null
            };
            if (key is not null) return Strings.Get(key);
            if (text.Contains("header")) return Strings.Get("Errors.InvalidHeaders");
            if (text.Contains("not found")) return Strings.Get("Errors.SourceMissing");
            if (text.Contains("permission") || text.Contains("access is denied")) return Strings.Get("Errors.FileWrite");
            if (text.Contains("unsupported") || text.Contains("invalid url")) return Strings.Get("Errors.InvalidLink");
            return Strings.Get("Errors.DownloadOperation");
        }
        if (error is HttpRequestException or TaskCanceledException) return Strings.Get("Errors.Connection");
        if (error is UnauthorizedAccessException) return Strings.Get("Errors.FileAccess");
        if (error is FormatException or IOException or TimeoutException) return error.Message;
        return Strings.Get("Errors.Operation");
    }
}
