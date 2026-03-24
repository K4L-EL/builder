using BetBuilder.Domain;

namespace BetBuilder.Application.Interfaces;

public interface ISelectionRuleFactory
{
    IReadOnlyList<SelectionRule> BuildRules(IReadOnlyList<string> legs);
}
