using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.Json;
using Harbor.Models;

internal static class DownloadChecks
{
    private static JsonObject Snapshot(int index) => new()
    {
        ["id"] = index.ToString(), ["name"] = $"下載-{index}.zip", ["status"] = "running",
        ["createdAt"] = "2026-10-08T00:00:00Z", ["protocol"] = "http", ["uploading"] = false,
        ["meta"] = new JsonObject
        {
            ["req"] = new JsonObject { ["url"] = $"https://example.com/下載-{index}.zip", ["extra"] = new JsonObject { ["header"] = new JsonObject { ["Referer"] = "https://example.com/" } } },
            ["opts"] = new JsonObject { ["path"] = "C:/Downloads", ["name"] = "", ["extra"] = new JsonObject { ["connections"] = 8 } },
            ["res"] = new JsonObject { ["size"] = 10000000L, ["files"] = new JsonArray(new JsonObject { ["name"] = $"下載-{index}.zip", ["size"] = 10000000L }) }
        },
        ["progress"] = new JsonObject { ["downloaded"] = 1000000L, ["speed"] = 200000L, ["uploaded"] = 0L, ["uploadSpeed"] = 0L }
    };

    public static void Run()
    {
        var statistics = JsonSerializer.Deserialize<Harbor.Models.TaskStatistics>("""
            {"snapshot":{"connections":[{"downloaded":512,"total":1024,"completed":false,"failed":false,"retryTimes":2}]},
             "runtime":{"activePeers":3,"peers":[{"address":"127.0.0.1:1234","client":"fixture","downloadSpeed":64,"completion":0.5}]}}
            """, JsonSerializerOptions.Web)!;
        if (statistics.Snapshot!.Connections[0].Downloaded != 512 || statistics.Snapshot.Connections[0].RetryTimes != 2 ||
            statistics.Runtime!.Peers[0].DownloadSpeed != 64 || statistics.Runtime.ActivePeers != 3)
            throw new Exception("Gopeed stats snapshot/runtime contract changed");

        var original = Snapshot(1);
        var item = new DownloadItem(original);
        var notified = new HashSet<string?>();
        item.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        if (item.Update(original.DeepClone().AsObject()) != DownloadChanges.None || notified.Count != 0)
            throw new Exception("An unchanged snapshot must preserve the row and avoid binding updates");

        var progress = original.DeepClone().AsObject(); progress["progress"]!["downloaded"] = 2000000L;
        if (item.Update(progress) != DownloadChanges.Progress || !notified.Contains(nameof(item.Percent)) || notified.Contains("") || item.Percent != 20)
            throw new Exception("Progress must update live metrics without resetting metadata bindings");

        var renamed = progress.DeepClone().AsObject(); renamed["name"] = "改名.zip";
        if ((item.Update(renamed) & DownloadChanges.Content) == 0 || item.Name != "改名.zip")
            throw new Exception("A name change must invalidate search and sort results");

        var seeding = renamed.DeepClone().AsObject(); seeding["status"] = "done"; seeding["uploading"] = true;
        if ((item.Update(seeding) & DownloadChanges.Content) == 0 || !item.CanPause || item.Percent != 100)
            throw new Exception("Completed torrents must still expose active seeding controls");

        var extracting = seeding.DeepClone().AsObject(); extracting["progress"]!["extractStatus"] = "extracting";
        if ((item.Update(extracting) & DownloadChanges.Content) == 0 || !item.IsProcessing)
            throw new Exception("Extraction must keep the task active after download completion");
        Console.WriteLine("Download checks passed: stable snapshots, progress notifications, rename, seeding and extraction transitions.");
    }

    public static void Benchmark()
    {
        const int count = 1000, rounds = 30;
        var before = Enumerable.Range(0, count).Select(Snapshot).ToArray();
        var after = before.Select(x => x.DeepClone().AsObject()).ToArray();
        var items = before.Select(x => new DownloadItem(x)).ToArray();
        Action legacy = () => { for (var i = 0; i < count; i++) _ = before[i].ToJsonString() == after[i].ToJsonString(); };
        Action current = () => { for (var i = 0; i < count; i++) _ = items[i].Update(after[i]); };
        Measure("JSON string comparison", legacy, rounds);
        Measure("Task snapshot update", current, rounds);
    }

    private static void Measure(string label, Action action, int rounds)
    {
        for (var i = 0; i < 3; i++) action();
        GC.Collect(); GC.WaitForPendingFinalizers();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < rounds; i++) action();
        watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine($"{label}: 1000 tasks x {rounds} snapshots; {watch.Elapsed.TotalMilliseconds:F1} ms, {allocated / 1048576.0:F2} MiB allocated");
    }
}
