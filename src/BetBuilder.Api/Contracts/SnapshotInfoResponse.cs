namespace BetBuilder.Api.Contracts;

public sealed class SnapshotInfoResponse
{
    public string SnapshotId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public string ModelVersion { get; init; } = default!;
    public int LegCount { get; init; }
    public int ScenarioCount { get; init; }
    public int UnavailableLegCount { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
}
