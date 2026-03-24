using BetBuilder.Domain;

namespace BetBuilder.Infrastructure.Snapshots;

public interface IPricingSnapshotFactory
{
    PricingSnapshot Build(SnapshotFileGroup fileGroup);
    PricingSnapshot BuildFromContent(SnapshotCsvContent content);
}

public sealed class SnapshotFileGroup
{
    public string SnapshotId { get; init; } = default!;
    public string OutcomeMatrixPath { get; init; } = default!;
    public string LegProbsPath { get; init; } = default!;
    public string CorrelationMatrixPath { get; init; } = default!;
}

public sealed class SnapshotCsvContent
{
    public string SnapshotId { get; init; } = default!;
    public string OutcomeMatrixCsv { get; init; } = default!;
    public string? LegProbsCsv { get; init; }
    public string? CorrelationMatrixCsv { get; init; }
    public string? EventId { get; init; }
    public string? ModelVersion { get; init; }
}
