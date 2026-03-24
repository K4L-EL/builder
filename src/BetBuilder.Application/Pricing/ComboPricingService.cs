using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Application.Pricing;

public sealed class ComboPricingService : IComboPricingService
{
    private readonly IActiveSnapshotStore _store;
    private readonly IComboValidator _validator;
    private readonly IJointProbabilityCalculator _calculator;
    private readonly IMarginService _marginService;

    public ComboPricingService(
        IActiveSnapshotStore store,
        IComboValidator validator,
        IJointProbabilityCalculator calculator,
        IMarginService marginService)
    {
        _store = store;
        _validator = validator;
        _calculator = calculator;
        _marginService = marginService;
    }

    public ComboPricingResult Price(ComboPricingRequest request)
    {
        var snapshot = ResolveSnapshot(request.SnapshotId);

        var validation = _validator.Validate(request.Legs, snapshot);
        if (validation.HasErrors)
            return ComboPricingResult.Invalid(snapshot.SnapshotId, validation.Errors);

        var indices = new int[request.Legs.Count];
        for (var i = 0; i < request.Legs.Count; i++)
            indices[i] = snapshot.LegIndexMap[request.Legs[i]];

        var probability = _calculator.Calculate(snapshot, indices);

        if (probability.MatchingScenarios == 0)
            return ComboPricingResult.ImpossibleCombo(
                snapshot.SnapshotId, probability.TotalScenarios, validation.Warnings);

        var margin = _marginService.Apply(probability.JointProbability);

        return ComboPricingResult.Success(
            snapshot.SnapshotId,
            Math.Round(probability.JointProbability, 6),
            margin.FairDecimalOdds,
            margin.PricedDecimalOdds,
            probability.MatchingScenarios,
            probability.TotalScenarios,
            validation.Warnings);
    }

    private PricingSnapshot ResolveSnapshot(string? snapshotId)
    {
        PricingSnapshot? snapshot;

        if (!string.IsNullOrEmpty(snapshotId))
        {
            snapshot = _store.GetSnapshot(snapshotId);
            if (snapshot == null)
                throw new KeyNotFoundException($"Snapshot '{snapshotId}' not found.");
        }
        else
        {
            snapshot = _store.GetActiveSnapshot();
            if (snapshot == null)
                throw new InvalidOperationException("No active snapshot available.");
        }

        return snapshot;
    }
}
