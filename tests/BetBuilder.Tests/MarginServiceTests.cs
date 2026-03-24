using BetBuilder.Application.Pricing;
using BetBuilder.Application.Validation;
using Microsoft.Extensions.Options;

namespace BetBuilder.Tests;

public class MarginServiceTests
{
    private static MarginService CreateService(
        double marginPercent = 5.0,
        double floor = 1.01,
        double cap = 1000.0,
        int decimalPlaces = 2)
    {
        var settings = Options.Create(new PricingSettings
        {
            MarginPercent = marginPercent,
            OddsFloor = floor,
            OddsCap = cap,
            OddsDecimalPlaces = decimalPlaces
        });
        return new MarginService(settings);
    }

    [Fact]
    public void Apply_StandardProbability_ReturnsCorrectOdds()
    {
        var service = CreateService(marginPercent: 5.0);

        var result = service.Apply(0.5);

        Assert.Equal(2.0, result.FairDecimalOdds);
        Assert.Equal(1.9, result.PricedDecimalOdds);
    }

    [Fact]
    public void Apply_HighProbability_RespectsFloor()
    {
        var service = CreateService(marginPercent: 99.0, floor: 1.01);

        var result = service.Apply(0.99);

        Assert.Equal(1.01, result.PricedDecimalOdds);
    }

    [Fact]
    public void Apply_LowProbability_RespectsCap()
    {
        var service = CreateService(marginPercent: 0.0, cap: 100.0);

        var result = service.Apply(0.001);

        Assert.Equal(100.0, result.PricedDecimalOdds);
    }

    [Fact]
    public void Apply_ZeroProbability_Throws()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.Apply(0.0));
    }

    [Fact]
    public void Apply_Rounding_RespectsDecimalPlaces()
    {
        var service = CreateService(marginPercent: 3.0, decimalPlaces: 2);

        var result = service.Apply(0.333);

        // fairOdds = 1/0.333 = 3.003003... -> rounded to 3.0
        // pricedOdds = 3.003003 * 0.97 = 2.912912... -> rounded to 2.91
        Assert.Equal(3.0, result.FairDecimalOdds);
        Assert.Equal(2.91, result.PricedDecimalOdds);
    }
}
