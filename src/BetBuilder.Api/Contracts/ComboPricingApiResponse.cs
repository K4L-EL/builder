namespace BetBuilder.Api.Contracts;

public sealed class ComboPricingApiResponse
{
    public bool Valid { get; init; }
    public string SnapshotId { get; init; } = default!;
    public double? JointProbability { get; init; }
    public double? FairDecimalOdds { get; init; }
    public double? PricedDecimalOdds { get; init; }
    public int? MatchingScenarios { get; init; }
    public int? TotalScenarios { get; init; }
    public IReadOnlyList<ValidationIssueDto> Errors { get; init; } = Array.Empty<ValidationIssueDto>();
    public IReadOnlyList<ValidationIssueDto> Warnings { get; init; } = Array.Empty<ValidationIssueDto>();
}

public sealed class ValidationIssueDto
{
    public string Code { get; init; } = default!;
    public string Message { get; init; } = default!;
}
