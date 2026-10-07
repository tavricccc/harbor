using System.Text;
using System.Text.Json.Nodes;
using Harbor.Services;

var payload = """{"req":{"url":"https://example.com/file?a=1&b=2","extra":{"header":{"Referer":"https://example.com/","Cookie":"key=value"}}},"opts":{"name":"測試下載.zip"}}""";
var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
var link = "gopeed:///create?params=" + Uri.EscapeDataString(encoded);
var parsed = GopeedLink.Parse(link);
if (parsed.Route != "create" || !JsonNode.DeepEquals(parsed.Parameters, JsonNode.Parse(payload))) throw new Exception("Payload mismatch");
if (GopeedLink.FromCommandLine("\"C:\\Program Files\\app.exe\" \"" + link + "\"") != link) throw new Exception("Command line mismatch");
if (GopeedLink.Parse("gopeed:///create").Parameters != null) throw new Exception("Empty create mismatch");
if (GopeedLink.Parse("gopeed:///extension?params=" + Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"url":"https://github.com/example/extension"}"""))).Route != "extension") throw new Exception("Extension mismatch");
if (GopeedLink.Parse("gopeed:///create?params=" + encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_')).Parameters?["opts"]?["name"]?.GetValue<string>() != "測試下載.zip") throw new Exception("Base64url mismatch");
try { GopeedLink.Parse("gopeed:///create?params=invalid!"); throw new Exception("Invalid payload accepted"); } catch (FormatException) { }
Console.WriteLine("Protocol checks passed: command line, UTF-8 payload, query escaping, headers, empty create, extension, base64url, invalid payload.");
foreach (var newline in new[] { "\r", "\n", "\r\n" })
{
    var headers = HttpHeaders.Parse("Sec-Ch-Ua: \"Chromium\";v=\"140\"" + newline + "Referer: https://www.virtualbox.org/wiki/Downloads" + newline + "Cookie: test=value");
    if (headers.Count != 3 || headers["Sec-Ch-Ua"]!.GetValue<string>() != "\"Chromium\";v=\"140\"" || headers["Cookie"]!.GetValue<string>() != "test=value") throw new Exception("Header line endings corrupted values");
    foreach (var value in headers.Select(pair => pair.Value!.GetValue<string>()))
        if (value.Contains('\r') || value.Contains('\n')) throw new Exception("Newline leaked into HTTP header value");
}
Console.WriteLine("Header checks passed: WinUI CR, LF and CRLF; Sec-Ch-Ua quotes, Referer and Cookie preserved.");
if (DownloadPresentation.ForStatus("done").Key != "open") throw new Exception("Completed download must prioritize opening the file");
if (DownloadPresentation.ForStatus("pause").Key != "continue" || DownloadPresentation.ForStatus("error").Label != "重試下載") throw new Exception("Paused/failed downloads must prioritize resume/retry");
if (DownloadPresentation.ForStatus("running").Key != "pause" || DownloadPresentation.ForStatus("unknown").Key != "none") throw new Exception("Running/unknown actions are incorrect");
Console.WriteLine("Action checks passed: completed Open, paused Resume, failed Retry, running Pause, unknown disabled.");
DownloadChecks.Run();
if (args.Contains("--benchmark")) DownloadChecks.Benchmark();
