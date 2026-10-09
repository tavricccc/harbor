using Harbor.Localization;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace Harbor.Services;

public sealed partial class CoreClient : IDisposable
{
    public static string DataDirectory
    {
        get;
    } = Path.GetFullPath(Environment.GetEnvironmentVariable("HARBOR_DATA_DIRECTORY")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Harbor"));
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private JsonObject session = null!;
    public string ApiAddress => $"http://127.0.0.1:{session["port"]}";
    public string Token => session["token"]!.GetValue<string>();
    public int ProcessId => session["pid"]!.GetValue<int>();
    public string Version { get; private set; } = "";

    public Task<JsonNode?> GetAsync(string route, CancellationToken cancellationToken = default) => SendAsync(HttpMethod.Get, route, cancellationToken: cancellationToken);
    public async Task<T?> GetAsync<T>(string route, CancellationToken cancellationToken = default) where T : class
        => (await GetAsync(route, cancellationToken))?.Deserialize<T>(System.Text.Json.JsonSerializerOptions.Web);
    public async Task<JsonNode?> SendAsync(HttpMethod method, string route, JsonNode? body = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, route);
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = (await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false))!.AsObject();
        if (result["code"]!.GetValue<int>() != 0)
            throw new DownloadApiException(result["msg"]!.GetValue<string>());
        result.Remove("data", out var data);
        return data;
    }
    public async Task<string> FetchTextAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "proxy");
        request.Headers.Add("X-Target-Uri", url);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task StopAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{session["controlPort"]}/shutdown");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    public void Dispose() => http.Dispose();
}
