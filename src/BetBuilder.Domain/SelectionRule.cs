namespace BetBuilder.Domain;

public sealed class SelectionRule
{
    public SelectionRuleType Type { get; init; }
    public string LegA { get; init; } = default!;
    public string LegB { get; init; } = default!;

    /// <summary>
    /// For Implication: LegA implies LegB (selecting LegA makes LegB redundant).
    /// For MutualExclusion: LegA and LegB cannot both be selected.
    /// </summary>
    public static SelectionRule Implication(string implies, string impliedBy) =>
        new() { Type = SelectionRuleType.Implication, LegA = implies, LegB = impliedBy };

    public static SelectionRule MutualExclusion(string legA, string legB) =>
        new() { Type = SelectionRuleType.MutualExclusion, LegA = legA, LegB = legB };
}

public enum SelectionRuleType
{
    Implication,
    MutualExclusion
}
