using BetBuilder.Api.Contracts;
using BetBuilder.Domain;

namespace BetBuilder.Api.Mapping;

public static class ResponseMapper
{
    public static ComboPricingApiResponse ToApiResponse(ComboPricingResult result) =>
        new()
        {
            Valid = result.Valid,
            SnapshotId = result.SnapshotId,
            JointProbability = result.JointProbability,
            FairDecimalOdds = result.FairDecimalOdds,
            PricedDecimalOdds = result.PricedDecimalOdds,
            MatchingScenarios = result.MatchingScenarios,
            TotalScenarios = result.TotalScenarios,
            Errors = result.Errors.Select(MapIssue).ToList(),
            Warnings = result.Warnings.Select(MapIssue).ToList()
        };

    public static SnapshotInfoResponse ToSnapshotInfo(PricingSnapshot snapshot) =>
        new()
        {
            SnapshotId = snapshot.SnapshotId,
            EventId = snapshot.EventId,
            ModelVersion = snapshot.ModelVersion,
            LegCount = snapshot.LegCount,
            ScenarioCount = snapshot.ScenarioCount,
            UnavailableLegCount = snapshot.UnavailableLegs.Count,
            GeneratedAtUtc = snapshot.GeneratedAtUtc
        };

    private static ValidationIssueDto MapIssue(ValidationIssue issue) =>
        new()
        {
            Code = issue.Code.ToString(),
            Message = issue.Message
        };
}
