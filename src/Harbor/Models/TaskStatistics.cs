namespace Harbor.Models;

public sealed record TaskStatistics(StatisticsSnapshot? Snapshot, StatisticsRuntime? Runtime);

public sealed record StatisticsSnapshot
{
    public HttpConnectionStatistics[] Connections { get; init; } = [];
    public long SeedBytes { get; init; }
    public double SeedRatio { get; init; }
    public long SeedTime { get; init; }
}

public sealed record StatisticsRuntime
{
    public int TotalPeers { get; init; }
    public int ActivePeers { get; init; }
    public int ConnectedSeeders { get; init; }
    public PeerStatistics[] Peers { get; init; } = [];
}

public sealed record HttpConnectionStatistics(long Downloaded, long Total, bool Completed, bool Failed, int RetryTimes);
public sealed record PeerStatistics(string Address, string Client, long DownloadSpeed, double? Completion);
