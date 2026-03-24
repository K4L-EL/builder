using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface IComboPricingService
{
    ComboPricingResult Price(ComboPricingRequest request);
}

public sealed class ComboPricingRequest
{
    public string? EventId { get; init; }
    public string? SnapshotId { get; init; }
    public IReadOnlyList<string> Legs { get; init; } = Array.Empty<string>();
}
