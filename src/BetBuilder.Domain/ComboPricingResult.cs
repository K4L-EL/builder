namespace BetBuilder.Domain;

public sealed class ComboPricingResult
{
    public bool Valid { get; init; }
    public string SnapshotId { get; init; } = default!;
    public double? JointProbability { get; init; }
    public double? FairDecimalOdds { get; init; }
    public double? PricedDecimalOdds { get; init; }
    public int? MatchingScenarios { get; init; }
    public int? TotalScenarios { get; init; }
    public IReadOnlyList<ValidationIssue> Errors { get; init; } = Array.Empty<ValidationIssue>();
    public IReadOnlyList<ValidationIssue> Warnings { get; init; } = Array.Empty<ValidationIssue>();

    public static ComboPricingResult Invalid(string snapshotId, IReadOnlyList<ValidationIssue> errors) =>
        new()
        {
            Valid = false,
            SnapshotId = snapshotId,
            Errors = errors
        };

    public static ComboPricingResult ImpossibleCombo(string snapshotId, int totalScenarios, IReadOnlyList<ValidationIssue> warnings) =>
        new()
        {
            Valid = true,
            SnapshotId = snapshotId,
            JointProbability = 0,
            FairDecimalOdds = null,
            PricedDecimalOdds = null,
            MatchingScenarios = 0,
            TotalScenarios = totalScenarios,
            Warnings = warnings
        };

    public static ComboPricingResult Success(
        string snapshotId,
        double jointProbability,
        double fairOdds,
        double pricedOdds,
        int matchingScenarios,
        int totalScenarios,
        IReadOnlyList<ValidationIssue> warnings) =>
        new()
        {
            Valid = true,
            SnapshotId = snapshotId,
            JointProbability = jointProbability,
            FairDecimalOdds = fairOdds,
            PricedDecimalOdds = pricedOdds,
            MatchingScenarios = matchingScenarios,
            TotalScenarios = totalScenarios,
            Warnings = warnings
        };
}
