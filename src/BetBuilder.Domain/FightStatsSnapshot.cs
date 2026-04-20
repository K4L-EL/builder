namespace BetBuilder.Domain;

/// <summary>
/// Schema-agnostic live stats slice for a fight at a point in time.
/// The exact columns/keys populated in <see cref="Metrics"/> and <see cref="LegResults"/>
/// depend on the CSV the simulator is feeding; consumers render whatever is present.
/// </summary>
public sealed class FightStatsSnapshot
{
    public string SnapshotId { get; init; } = default!;
    public string EventId { get; init; } = default!;
    public double ElapsedSeconds { get; init; }
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// All numeric stat columns from the row, keyed by header. Includes per-fighter
    /// metrics (e.g. "red_health", "blue_strikes", "round") as well as any custom fields.
    /// </summary>
    public IReadOnlyDictionary<string, double> Metrics { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// Optional explicit leg-outcome signal if the CSV carries columns named
    /// <c>bb_&lt;leg&gt;_result</c> (1 = won, 0 = lost, -1 = void). Used by the
    /// resulting service when present; falls back to probability thresholds otherwise.
    /// </summary>
    public IReadOnlyDictionary<string, int> LegResults { get; init; } =
        new Dictionary<string, int>();
}
