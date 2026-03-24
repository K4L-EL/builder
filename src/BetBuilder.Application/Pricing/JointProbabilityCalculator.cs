using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Application.Pricing;

public sealed class JointProbabilityCalculator : IJointProbabilityCalculator
{
    public JointProbabilityResult Calculate(PricingSnapshot snapshot, int[] legIndices)
    {
        var matrix = snapshot.OutcomeMatrix;
        var totalRows = matrix.Length;
        var legCount = legIndices.Length;
        var hits = 0;

        for (var row = 0; row < totalRows; row++)
        {
            var rowData = matrix[row];
            var allHit = true;

            for (var i = 0; i < legCount; i++)
            {
                if (rowData[legIndices[i]] == 0)
                {
                    allHit = false;
                    break;
                }
            }

            if (allHit)
                hits++;
        }

        return new JointProbabilityResult
        {
            MatchingScenarios = hits,
            TotalScenarios = totalRows,
            JointProbability = totalRows > 0 ? (double)hits / totalRows : 0
        };
    }
}
