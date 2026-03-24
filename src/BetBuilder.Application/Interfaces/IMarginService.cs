namespace BetBuilder.Application.Interfaces;

public interface IMarginService
{
    MarginResult Apply(double jointProbability);
}

public readonly struct MarginResult
{
    public double FairDecimalOdds { get; init; }
    public double PricedDecimalOdds { get; init; }
}
