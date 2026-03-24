using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface IJointProbabilityCalculator
{
    JointProbabilityResult Calculate(PricingSnapshot snapshot, int[] legIndices);
}

public readonly struct JointProbabilityResult
{
    public int MatchingScenarios { get; init; }
    public int TotalScenarios { get; init; }
    public double JointProbability { get; init; }
}
