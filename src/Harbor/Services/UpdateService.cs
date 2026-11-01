using Harbor.Localization;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Harbor.Services;

/// <summary>Idle: not checked yet. Ready: the installer is downloaded and verified, waiting for Install.
/// Failed: the last step failed, with the reason in <see cref="UpdateService.Error"/>.</summary>
public enum UpdateState { Idle, Checking, UpToDate, Available, Downloading, Ready, Failed }

public sealed record UpdateRelease(string Version, string Notes, DateTimeOffset PublishedAt, long SizeBytes, string PageUrl, bool Prerelease)
{
    internal Uri Package { get; init; } = null!;
    internal Uri Sums { get; init; } = null!;
    internal string PackageName { get; init; } = "";
}

/// <summary>
/// Finds the newest stable release and, when newer than that, the newest pre-release on GitHub; downloads the chosen
/// single-file installer itself, checks it against the release's SHA-256 file, then starts it with <c>--auto-update</c>
/// so the setup installs and reopens Harbor. Ported from Shanjie's updater.
/// </summary>
public sealed partial class UpdateService : IDisposable
{
    private static readonly Uri Source = new("https://api.github.com/repos/tavricccc/harbor/releases?per_page=30");
    private static UpdateService? shared;
    private readonly HttpClient http;
    private readonly string directory = Path.Combine(CoreClient.DataDirectory, "Updates");
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private DateTimeOffset retryAfter;
    private string? readyPath;
    private bool disposed;

    /// <summary>One service for the app, so the main page and Settings show the same check and download.</summary>
    public static UpdateService Shared => shared ??= new UpdateService();
    public string CurrentVersion { get; } = RunningVersion();
    public UpdateState State { get; private set; }
    /// <summary>The release to download: the stable one by default, else the pre-release.</summary>
    public UpdateRelease? Latest { get; private set; }
    /// <summary>Newer releases to choose from: stable first, pre-release second, at most one each.</summary>
    public IReadOnlyList<UpdateRelease> Choices { get; private set; } = [];
    public double DownloadProgress { get; private set; }
    public string? Error { get; private set; }
    /// <summary>Any state change; may fire on a background thread.</summary>
    public event EventHandler? Changed;

    private UpdateService()
    {
        http = new(new HttpClientHandler { UseCookies = false, UseDefaultCredentials = false, AllowAutoRedirect = false });
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Harbor/" + CurrentVersion);
        http.Timeout = TimeSpan.FromMinutes(10);
    }

    private static string RunningVersion()
    {
        var version = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
        return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }

    /// <summary>Compares dotted numbers part by part; positive when a is newer.</summary>
    public static int Compare(string a, string b)
    {
        int[] x = a.Split('.').Select(int.Parse).ToArray(), y = b.Split('.').Select(int.Parse).ToArray();
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int difference = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (difference != 0)
                return difference;
        }
        return 0;
    }

    private void Set(UpdateState state, string? error = null)
    {
        State = state;
        Error = error;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || !await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            if (DateTimeOffset.UtcNow < retryAfter)
            {
                Set(UpdateState.Failed, Strings.Get("Update.ErrorRateLimit"));
                return;
            }
            Set(UpdateState.Checking);
            using var response = await GetAsync(Source, cancel.Token).ConfigureAwait(false);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancel.Token).ConfigureAwait(false));
            Latest = null;
            readyPath = null;
            var found = json.RootElement.EnumerateArray().Select(Parse).OfType<UpdateRelease>()
                .Where(release => Compare(release.Version, CurrentVersion) > 0).ToList();
            var order = Comparer<string>.Create(Compare);
            var stable = found.Where(release => !release.Prerelease).MaxBy(release => release.Version, order);
            var preview = found.Where(release => release.Prerelease && (stable is null || Compare(release.Version, stable.Version) > 0)).MaxBy(release => release.Version, order);
            Choices = new[] { stable, preview }.OfType<UpdateRelease>().ToArray();
            if (Choices.Count == 0)
            {
                Set(UpdateState.UpToDate);
                return;
            }
            Latest = Choices[0];
            Set(UpdateState.Available);
        }
        catch (OperationCanceledException)
        {
            Set(cancel.IsCancellationRequested ? UpdateState.Idle : UpdateState.Failed, cancel.IsCancellationRequested ? null : Strings.Get("Update.ErrorTimeout"));
        }
        catch (Exception error) when (Expected(error)) { Set(UpdateState.Failed, Message(error)); }
        finally { gate.Release(); }
    }

    /// <summary>Picks the other release before a download starts or after one failed.</summary>
    public void Choose(UpdateRelease release)
    {
        if (!Choices.Contains(release) || State is UpdateState.Checking or UpdateState.Downloading)
            return;
        Latest = release;
        readyPath = null;
        Set(UpdateState.Available);
    }

    /// <summary>A release, or null for drafts, odd version tags and releases without the installer and its .sha256.</summary>
    private static UpdateRelease? Parse(JsonElement root)
    {
        if (root.GetProperty("draft").GetBoolean())
            return null;
        var version = root.GetProperty("tag_name").GetString()!.TrimStart('v');
        if (!VersionPattern().IsMatch(version))
            return null;
        var name = $"Harbor-Setup-{version}-x64.exe";
        var files = root.GetProperty("assets").EnumerateArray().ToArray();
        var package = files.FirstOrDefault(file => file.GetProperty("name").GetString() == name);
        var sums = files.FirstOrDefault(file => file.GetProperty("name").GetString() == name + ".sha256");
        if (package.ValueKind == JsonValueKind.Undefined || sums.ValueKind == JsonValueKind.Undefined)
            return null;
        return new(version, PlainNotes(root.GetProperty("body").GetString() ?? ""), root.GetProperty("published_at").GetDateTimeOffset(),
            package.GetProperty("size").GetInt64(), ValidateUrl(root.GetProperty("html_url").GetString()!).AbsoluteUri, root.GetProperty("prerelease").GetBoolean())
        {
            Package = ValidateUrl(package.GetProperty("browser_download_url").GetString()!),
            Sums = ValidateUrl(sums.GetProperty("browser_download_url").GetString()!),
            PackageName = name,
        };
    }

    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || !await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            if (Latest is not null)
                await DownloadCore(Latest, cancel.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Set(cancel.IsCancellationRequested ? UpdateState.Available : UpdateState.Failed, cancel.IsCancellationRequested ? null : Strings.Get("Update.ErrorTimeout"));
        }
        catch (Exception error) when (Expected(error)) { Set(UpdateState.Failed, Message(error)); }
        finally { gate.Release(); }
    }

    private async Task DownloadCore(UpdateRelease release, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, release.PackageName), partial = target + ".partial";
        readyPath = null;
        DownloadProgress = 0;
        Set(UpdateState.Downloading);
        try
        {
            using var checksum = await GetAsync(release.Sums, token).ConfigureAwait(false);
            var match = ChecksumPattern().Match((await checksum.Content.ReadAsStringAsync(token).ConfigureAwait(false)).Trim());
            if (!match.Success || match.Groups[2].Value.Trim() != release.PackageName)
                throw new InvalidDataException();
            var hash = match.Groups[1].Value;
            if (File.Exists(target) && await Valid(target, release, hash, token).ConfigureAwait(false))
            {
                Ready(target);
                return;
            }
            File.Delete(target);
            using var response = await GetAsync(release.Package, token).ConfigureAwait(false);
            await using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            {
                var buffer = new byte[65536];
                long received = 0;
                double notified = 0;
                var watch = Stopwatch.StartNew();
                int read;
                while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    received += read;
                    var progress = release.SizeBytes > 0 ? Math.Min(1, (double)received / release.SizeBytes) : 0;
                    if (progress - notified >= .01 || watch.ElapsedMilliseconds >= 200)
                    {
                        DownloadProgress = notified = progress;
                        watch.Restart();
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            if (!await Valid(partial, release, hash, token).ConfigureAwait(false))
                throw new InvalidDataException();
            File.Move(partial, target, true);
            Ready(target);
        }
        finally { File.Delete(partial); }
    }

    private static async Task<bool> Valid(string path, UpdateRelease release, string hash, CancellationToken token)
    {
        if (new FileInfo(path).Length != release.SizeBytes)
            return false;
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false)).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }

    private void Ready(string path)
    {
        // Keep only this download's installer.
        foreach (var old in Directory.EnumerateFiles(directory, "Harbor-Setup-*.exe"))
            if (old != path)
                File.Delete(old);
        readyPath = path;
        DownloadProgress = 1;
        Set(UpdateState.Ready);
    }

    private static Uri ValidateUrl(string text)
    {
        var url = new Uri(text);
        var allowed = url.Scheme == "https" && (url.Host == "github.com" || url.Host == "api.github.com"
            || url.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));
        if (!allowed || url.UserInfo.Length != 0)
            throw new InvalidDataException();
        return url;
    }

    private async Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken token)
    {
        for (int redirects = 0; redirects < 6; redirects++)
        {
            var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if ((int)response.StatusCode is 403 or 429)
            {
                retryAfter = DateTimeOffset.UtcNow.AddHours(1);
                if (response.Headers.RetryAfter?.Delta is { } delay && DateTimeOffset.UtcNow + delay > retryAfter)
                    retryAfter = DateTimeOffset.UtcNow + delay;
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var seconds)
                    && DateTimeOffset.FromUnixTimeSeconds(seconds) is var reset && reset > retryAfter)
                    retryAfter = reset;
                response.Dispose();
                throw new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests);
            }
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();
                url = ValidateUrl(new Uri(url, location ?? throw new InvalidDataException()).AbsoluteUri);
                continue;
            }
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode;
                response.Dispose();
                throw new HttpRequestException("request failed", null, status);
            }
            return response;
        }
        throw new HttpRequestException("too many redirects");
    }

    /// <summary>Release notes without Markdown, capped for display.</summary>
    public static string PlainNotes(string notes)
    {
        notes = Regex.Replace(notes, @"!?\[([^\]]*)\]\([^)]*\)", "$1");
        notes = Regex.Replace(notes, @"(?m)^\s{0,3}(#{1,6}\s*|>\s*|[-*+]\s+)", "");
        notes = Regex.Replace(notes, @"<[^>]+>|[`*_~]", "").Trim();
        return notes[..Math.Min(2000, notes.Length)];
    }

    private static bool Expected(Exception error) => error is InvalidDataException or HttpRequestException or IOException or UnauthorizedAccessException
        or JsonException or InvalidOperationException or FormatException or ArgumentException or KeyNotFoundException;

    private static string Message(Exception error) => Strings.Get(error switch
    {
        InvalidDataException => "Update.ErrorCorrupt",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "Update.ErrorRateLimit",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "Update.ErrorNotFound",
        HttpRequestException => "Update.ErrorNetwork",
        UnauthorizedAccessException or IOException => "Update.ErrorDisk",
        _ => "Update.ErrorFormat",
    });

    /// <summary>Only when Ready: starts the installer in auto-update mode. True when it started; the caller then quits Harbor.</summary>
    public bool Install()
    {
        if (disposed || State != UpdateState.Ready || readyPath is null)
            return false;
        try
        {
            var start = new ProcessStartInfo(readyPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(readyPath)! };
            start.ArgumentList.Add("--auto-update");
            using var setup = Process.Start(start) ?? throw new InvalidOperationException();
            return true;
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Set(UpdateState.Failed, Strings.Get("Update.ErrorLaunch"));
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        lifetime.Cancel();
        http.Dispose();
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern();
    [GeneratedRegex(@"^([a-fA-F0-9]{64})\s+\*?(.+)$")]
    private static partial Regex ChecksumPattern();
}
