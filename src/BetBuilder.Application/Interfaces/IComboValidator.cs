using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface IComboValidator
{
    ValidationResult Validate(IReadOnlyList<string> legs, PricingSnapshot snapshot);
}

public sealed class ValidationResult
{
    public IReadOnlyList<ValidationIssue> Errors { get; init; } = Array.Empty<ValidationIssue>();
    public IReadOnlyList<ValidationIssue> Warnings { get; init; } = Array.Empty<ValidationIssue>();
    public bool HasErrors => Errors.Count > 0;
}
