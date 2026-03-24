namespace BetBuilder.Domain;

public sealed class PricingSnapshot
{
    public string SnapshotId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public string ModelVersion { get; init; } = default!;
    public DateTime GeneratedAtUtc { get; init; }

    public IReadOnlyList<string> Legs { get; init; } = default!;
    public IReadOnlyDictionary<string, int> LegIndexMap { get; init; } = default!;

    public double[] LegProbabilities { get; init; } = default!;
    public double?[,] CorrelationMatrix { get; init; } = default!;
    public byte[][] OutcomeMatrix { get; init; } = default!;

    public IReadOnlySet<string> UnavailableLegs { get; init; } = default!;

    public int ScenarioCount => OutcomeMatrix.Length;
    public int LegCount => Legs.Count;
}
