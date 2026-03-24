using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;
using Microsoft.Extensions.Options;

namespace BetBuilder.Application.Validation;

public sealed class PricingSettings
{
    public const string SectionName = "Pricing";
    public double MarginPercent { get; set; } = 5.0;
    public double OddsFloor { get; set; } = 1.01;
    public double OddsCap { get; set; } = 1000.0;
    public int MaxLegs { get; set; } = 12;
    public int OddsDecimalPlaces { get; set; } = 2;
}

public sealed class ComboValidator : IComboValidator
{
    private readonly ISelectionRuleFactory _ruleFactory;
    private readonly PricingSettings _settings;

    public ComboValidator(ISelectionRuleFactory ruleFactory, IOptions<PricingSettings> settings)
    {
        _ruleFactory = ruleFactory;
        _settings = settings.Value;
    }

    public ValidationResult Validate(IReadOnlyList<string> legs, PricingSnapshot snapshot)
    {
        var errors = new List<ValidationIssue>();
        var warnings = new List<ValidationIssue>();

        if (legs.Count > _settings.MaxLegs)
        {
            errors.Add(ValidationIssue.Error(
                ValidationIssueCode.MaxLegsExceeded,
                $"Maximum {_settings.MaxLegs} legs allowed, got {legs.Count}."));
            return new ValidationResult { Errors = errors, Warnings = warnings };
        }

        var seen = new HashSet<string>();
        foreach (var leg in legs)
        {
            if (!seen.Add(leg))
            {
                errors.Add(ValidationIssue.Error(
                    ValidationIssueCode.DuplicateLeg,
                    $"Duplicate selection: '{leg}'."));
            }
        }

        foreach (var leg in legs)
        {
            if (!snapshot.LegIndexMap.ContainsKey(leg))
            {
                errors.Add(ValidationIssue.Error(
                    ValidationIssueCode.UnknownLeg,
                    $"Unknown leg: '{leg}'."));
            }
            else if (snapshot.UnavailableLegs.Contains(leg))
            {
                errors.Add(ValidationIssue.Error(
                    ValidationIssueCode.UnavailableLeg,
                    $"Leg '{leg}' is unavailable (zero probability in current snapshot)."));
            }
        }

        if (errors.Count > 0)
            return new ValidationResult { Errors = errors, Warnings = warnings };

        var rules = _ruleFactory.BuildRules(snapshot.Legs);
        var selectedSet = new HashSet<string>(legs);

        foreach (var rule in rules)
        {
            if (!selectedSet.Contains(rule.LegA) || !selectedSet.Contains(rule.LegB))
                continue;

            switch (rule.Type)
            {
                case SelectionRuleType.MutualExclusion:
                    errors.Add(ValidationIssue.Error(
                        ValidationIssueCode.MutuallyExclusive,
                        $"'{rule.LegA}' and '{rule.LegB}' are mutually exclusive."));
                    break;

                case SelectionRuleType.Implication:
                    warnings.Add(ValidationIssue.Warning(
                        ValidationIssueCode.RedundantSelection,
                        $"'{rule.LegA}' implies '{rule.LegB}' -- redundant selection."));
                    break;
            }
        }

        return new ValidationResult { Errors = errors, Warnings = warnings };
    }
}
