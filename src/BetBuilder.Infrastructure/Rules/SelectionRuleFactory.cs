using System.Text.RegularExpressions;
using BetBuilder.Application.Interfaces;
using BetBuilder.Domain;

namespace BetBuilder.Infrastructure.Rules;

public sealed class SelectionRuleFactory : ISelectionRuleFactory
{
    private static readonly Regex ThresholdPattern = new(
        @"^bb_(red|blue)_(phsl|tsl)_over_(\d+_\d+)$",
        RegexOptions.Compiled);

    public IReadOnlyList<SelectionRule> BuildRules(IReadOnlyList<string> legs)
    {
        var legSet = new HashSet<string>(legs);
        var rules = new List<SelectionRule>();

        BuildOutcomeRules(legSet, rules);
        BuildThresholdImplicationRules(legs, rules);

        return rules;
    }

    private static void BuildOutcomeRules(HashSet<string> legSet, List<SelectionRule> rules)
    {
        // Mutual exclusions: result legs
        AddExclusionIfPresent(legSet, rules, "bb_red_win", "bb_blue_win");
        AddExclusionIfPresent(legSet, rules, "bb_red_win", "bb_draw");
        AddExclusionIfPresent(legSet, rules, "bb_blue_win", "bb_draw");

        // Mutual exclusions: method-of-victory cross-corner
        AddExclusionIfPresent(legSet, rules, "bb_red_ko", "bb_blue_ko");
        AddExclusionIfPresent(legSet, rules, "bb_red_ko", "bb_blue_decision");
        AddExclusionIfPresent(legSet, rules, "bb_red_decision", "bb_blue_ko");
        AddExclusionIfPresent(legSet, rules, "bb_red_decision", "bb_blue_decision");

        // Mutual exclusions: method-of-victory same corner
        AddExclusionIfPresent(legSet, rules, "bb_red_ko", "bb_red_decision");
        AddExclusionIfPresent(legSet, rules, "bb_blue_ko", "bb_blue_decision");

        // Mutual exclusions: method vs opposing win
        AddExclusionIfPresent(legSet, rules, "bb_red_ko", "bb_blue_win");
        AddExclusionIfPresent(legSet, rules, "bb_red_ko", "bb_draw");
        AddExclusionIfPresent(legSet, rules, "bb_red_decision", "bb_blue_win");
        AddExclusionIfPresent(legSet, rules, "bb_red_decision", "bb_draw");
        AddExclusionIfPresent(legSet, rules, "bb_blue_ko", "bb_red_win");
        AddExclusionIfPresent(legSet, rules, "bb_blue_ko", "bb_draw");
        AddExclusionIfPresent(legSet, rules, "bb_blue_decision", "bb_red_win");
        AddExclusionIfPresent(legSet, rules, "bb_blue_decision", "bb_draw");

        // Mutual exclusions: win_or_draw combos
        AddExclusionIfPresent(legSet, rules, "bb_red_win_or_draw", "bb_blue_win");
        AddExclusionIfPresent(legSet, rules, "bb_blue_win_or_draw", "bb_red_win");

        // Implications: KO/decision implies win
        foreach (var side in new[] { "red", "blue" })
        {
            AddImplicationIfPresent(legSet, rules, $"bb_{side}_ko", $"bb_{side}_win");
            AddImplicationIfPresent(legSet, rules, $"bb_{side}_decision", $"bb_{side}_win");
            AddImplicationIfPresent(legSet, rules, $"bb_{side}_win", $"bb_{side}_win_or_draw");
            AddImplicationIfPresent(legSet, rules, $"bb_{side}_ko", $"bb_{side}_win_or_draw");
            AddImplicationIfPresent(legSet, rules, $"bb_{side}_decision", $"bb_{side}_win_or_draw");
        }

        // draw implies both win_or_draw
        AddImplicationIfPresent(legSet, rules, "bb_draw", "bb_red_win_or_draw");
        AddImplicationIfPresent(legSet, rules, "bb_draw", "bb_blue_win_or_draw");
    }

    private static void BuildThresholdImplicationRules(IReadOnlyList<string> legs, List<SelectionRule> rules)
    {
        // Group threshold legs by (side, metric) and generate nested implications
        var groups = new Dictionary<string, List<(string leg, double threshold)>>();

        foreach (var leg in legs)
        {
            var match = ThresholdPattern.Match(leg);
            if (!match.Success)
                continue;

            var side = match.Groups[1].Value;
            var metric = match.Groups[2].Value;
            var thresholdStr = match.Groups[3].Value.Replace('_', '.');
            var threshold = double.Parse(thresholdStr, System.Globalization.CultureInfo.InvariantCulture);
            var key = $"{side}_{metric}";

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<(string, double)>();
                groups[key] = list;
            }

            list.Add((leg, threshold));
        }

        // Also handle knockdown thresholds
        var knockdownLegs = new List<(string leg, double threshold)>();
        var knockdownPattern = new Regex(@"^bb_knockdowns_over_(\d+_\d+)$", RegexOptions.Compiled);
        foreach (var leg in legs)
        {
            var match = knockdownPattern.Match(leg);
            if (!match.Success)
                continue;

            var thresholdStr = match.Groups[1].Value.Replace('_', '.');
            var threshold = double.Parse(thresholdStr, System.Globalization.CultureInfo.InvariantCulture);
            knockdownLegs.Add((leg, threshold));
        }

        if (knockdownLegs.Count > 0)
            groups["knockdowns"] = knockdownLegs;

        foreach (var (_, thresholds) in groups)
        {
            var sorted = thresholds.OrderByDescending(t => t.threshold).ToList();

            // Higher threshold implies all lower thresholds:
            // over_94.5 implies over_67.5, over_67.5 implies over_40.5
            for (var i = 0; i < sorted.Count - 1; i++)
            {
                rules.Add(SelectionRule.Implication(sorted[i].leg, sorted[i + 1].leg));
            }
        }
    }

    private static void AddExclusionIfPresent(
        HashSet<string> legSet, List<SelectionRule> rules, string a, string b)
    {
        if (legSet.Contains(a) && legSet.Contains(b))
            rules.Add(SelectionRule.MutualExclusion(a, b));
    }

    private static void AddImplicationIfPresent(
        HashSet<string> legSet, List<SelectionRule> rules, string implies, string implied)
    {
        if (legSet.Contains(implies) && legSet.Contains(implied))
            rules.Add(SelectionRule.Implication(implies, implied));
    }
}
