using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Application.Resulting;

/// <summary>
/// Maps a leg name to a win/lose/void outcome using the final snapshot probabilities
/// and (optionally) an explicit leg-result column in the stats CSV.
/// </summary>
public interface ILegOutcomeResolver
{
    LegOutcomeResult Resolve(string legName, PricingSnapshot finalSnapshot, FightStatsSnapshot? finalStats);
}

public readonly record struct LegOutcomeResult(LegOutcome Outcome, double? FinalProbability, string Reason);

/// <summary>
/// Default resolver: trust the explicit <c>bb_&lt;leg&gt;_result</c> column when present,
/// otherwise fall back to a probability threshold (>=0.99 won, &lt;=0.01 lost, else void).
/// </summary>
public sealed class DefaultLegOutcomeResolver : ILegOutcomeResolver
{
    private const double WinThreshold = 0.99;
    private const double LoseThreshold = 0.01;

    public LegOutcomeResult Resolve(string legName, PricingSnapshot finalSnapshot, FightStatsSnapshot? finalStats)
    {
        if (finalStats != null && finalStats.LegResults.TryGetValue(legName, out var explicitResult))
        {
            var outcome = explicitResult switch
            {
                > 0 => LegOutcome.Won,
                0 => LegOutcome.Lost,
                < 0 => LegOutcome.Void
            };
            return new LegOutcomeResult(outcome, null, "explicit-csv");
        }

        if (!finalSnapshot.LegIndexMap.TryGetValue(legName, out var idx))
            return new LegOutcomeResult(LegOutcome.Void, null, "leg-not-in-snapshot");

        var prob = finalSnapshot.LegProbabilities[idx];

        if (prob >= WinThreshold) return new LegOutcomeResult(LegOutcome.Won, prob, "prob-threshold-won");
        if (prob <= LoseThreshold) return new LegOutcomeResult(LegOutcome.Lost, prob, "prob-threshold-lost");
        return new LegOutcomeResult(LegOutcome.Void, prob, "prob-ambiguous");
    }
}
