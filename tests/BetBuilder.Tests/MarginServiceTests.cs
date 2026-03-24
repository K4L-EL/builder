using BetBuilder.Application.Pricing;
using BetBuilder.Application.Validation;
using Microsoft.Extensions.Options;

namespace BetBuilder.Tests;

public class MarginServiceTests
{
    private static MarginService CreateService(
        double baseMargin = 5.0,
        double scaleFactor = 7.5,
        double maxMargin = 50.0,
        double floor = 1.01,
        double cap = 1000.0,
        int decimalPlaces = 2)
    {
        var settings = Options.Create(new PricingSettings
        {
            BaseMarginPercent = baseMargin,
            MarginScaleFactor = scaleFactor,
            MaxMarginPercent = maxMargin,
            OddsFloor = floor,
            OddsCap = cap,
            OddsDecimalPlaces = decimalPlaces
        });
        return new MarginService(settings);
    }

    [Fact]
    public void Apply_ShortOdds_GetsSmallMargin()
    {
        var service = CreateService();

        // prob=0.938, fair=1.066 -> margin ≈ 5 + 7.5*ln(1.066) ≈ 5.5%
        var result = service.Apply(0.938);

        Assert.True(result.PricedDecimalOdds >= 1.01);
        Assert.True(result.PricedDecimalOdds < result.FairDecimalOdds);
    }

    [Fact]
    public void Apply_MediumOdds_GetsModerateMargin()
    {
        var service = CreateService();

        // prob=0.5, fair=2.0 -> margin ≈ 5 + 7.5*ln(2.0) ≈ 10.2%
        var result = service.Apply(0.5);

        Assert.Equal(2.0, result.FairDecimalOdds);
        var effectiveMargin = 1.0 - (result.PricedDecimalOdds / result.FairDecimalOdds);
        Assert.InRange(effectiveMargin, 0.08, 0.15);
    }

    [Fact]
    public void Apply_LongOdds_GetsHigherMargin()
    {
        var service = CreateService();

        // prob=0.01, fair=100 -> margin ≈ 5 + 7.5*ln(100) ≈ 39.5%
        var result = service.Apply(0.01);

        var effectiveMargin = 1.0 - (result.PricedDecimalOdds / result.FairDecimalOdds);
        Assert.InRange(effectiveMargin, 0.30, 0.45);
    }

    [Fact]
    public void Apply_VeryLongOdds_CappedAtMaxMargin()
    {
        var service = CreateService(maxMargin: 50.0);

        // prob=0.001, fair=1000 -> margin would be 5+7.5*ln(1000)=56.8%, capped at 50%
        var result = service.Apply(0.001);

        var effectiveMargin = 1.0 - (result.PricedDecimalOdds / result.FairDecimalOdds);
        Assert.InRange(effectiveMargin, 0.49, 0.51);
    }

    [Fact]
    public void Apply_MarginScalesWithOdds()
    {
        var service = CreateService();

        var short_ = service.Apply(0.9);
        var medium = service.Apply(0.3);
        var long_  = service.Apply(0.05);

        var marginShort  = 1.0 - (short_.PricedDecimalOdds / short_.FairDecimalOdds);
        var marginMedium = 1.0 - (medium.PricedDecimalOdds / medium.FairDecimalOdds);
        var marginLong   = 1.0 - (long_.PricedDecimalOdds / long_.FairDecimalOdds);

        Assert.True(marginShort < marginMedium);
        Assert.True(marginMedium < marginLong);
    }

    [Fact]
    public void Apply_HighProbability_RespectsFloor()
    {
        var service = CreateService(baseMargin: 90.0, floor: 1.01);

        var result = service.Apply(0.99);

        Assert.Equal(1.01, result.PricedDecimalOdds);
    }

    [Fact]
    public void Apply_LowProbability_RespectsCap()
    {
        var service = CreateService(baseMargin: 0.0, scaleFactor: 0.0, cap: 100.0);

        var result = service.Apply(0.001);

        Assert.Equal(100.0, result.PricedDecimalOdds);
    }

    [Fact]
    public void Apply_ZeroProbability_Throws()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.Apply(0.0));
    }
}
