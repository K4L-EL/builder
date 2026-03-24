using BetBuilder.Application.Interfaces;
using BetBuilder.Application.Validation;
using Microsoft.Extensions.Options;

namespace BetBuilder.Application.Pricing;

public sealed class MarginService : IMarginService
{
    private readonly PricingSettings _settings;

    public MarginService(IOptions<PricingSettings> settings)
    {
        _settings = settings.Value;
    }

    public MarginResult Apply(double jointProbability)
    {
        if (jointProbability <= 0)
            throw new ArgumentException("Joint probability must be positive.", nameof(jointProbability));

        var fairOdds = 1.0 / jointProbability;

        // Scaled margin: longer odds get proportionally higher margin
        var marginPercent = _settings.BaseMarginPercent
                          + _settings.MarginScaleFactor * Math.Log(Math.Max(fairOdds, 1.0));
        marginPercent = Math.Min(marginPercent, _settings.MaxMarginPercent);

        var pricedOdds = fairOdds * (1.0 - marginPercent / 100.0);

        pricedOdds = Math.Max(pricedOdds, _settings.OddsFloor);
        pricedOdds = Math.Min(pricedOdds, _settings.OddsCap);
        pricedOdds = Math.Round(pricedOdds, _settings.OddsDecimalPlaces);
        fairOdds = Math.Round(fairOdds, _settings.OddsDecimalPlaces);

        return new MarginResult
        {
            FairDecimalOdds = fairOdds,
            PricedDecimalOdds = pricedOdds
        };
    }
}
