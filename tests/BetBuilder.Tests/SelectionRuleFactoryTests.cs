using BetBuilder.Domain;
using BetBuilder.Infrastructure.Rules;

namespace BetBuilder.Tests;

public class SelectionRuleFactoryTests
{
    private readonly SelectionRuleFactory _factory = new();

    [Fact]
    public void BuildRules_GeneratesMutualExclusions_ForResultLegs()
    {
        var legs = new[] { "bb_red_win", "bb_blue_win", "bb_draw" };

        var rules = _factory.BuildRules(legs);

        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.MutualExclusion &&
            ((r.LegA == "bb_red_win" && r.LegB == "bb_blue_win") ||
             (r.LegA == "bb_blue_win" && r.LegB == "bb_red_win")));
    }

    [Fact]
    public void BuildRules_GeneratesImplications_ForKoImpliesWin()
    {
        var legs = new[] { "bb_red_win", "bb_red_ko" };

        var rules = _factory.BuildRules(legs);

        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.Implication &&
            r.LegA == "bb_red_ko" && r.LegB == "bb_red_win");
    }

    [Fact]
    public void BuildRules_GeneratesThresholdImplications()
    {
        var legs = new[]
        {
            "bb_red_tsl_over_40_5",
            "bb_red_tsl_over_67_5",
            "bb_red_tsl_over_94_5"
        };

        var rules = _factory.BuildRules(legs);

        // over_94.5 implies over_67.5
        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.Implication &&
            r.LegA == "bb_red_tsl_over_94_5" && r.LegB == "bb_red_tsl_over_67_5");

        // over_67.5 implies over_40.5
        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.Implication &&
            r.LegA == "bb_red_tsl_over_67_5" && r.LegB == "bb_red_tsl_over_40_5");
    }

    [Fact]
    public void BuildRules_GeneratesKnockdownThresholdImplications()
    {
        var legs = new[]
        {
            "bb_knockdowns_over_0_5",
            "bb_knockdowns_over_1_5",
            "bb_knockdowns_over_2_5"
        };

        var rules = _factory.BuildRules(legs);

        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.Implication &&
            r.LegA == "bb_knockdowns_over_2_5" && r.LegB == "bb_knockdowns_over_1_5");

        Assert.Contains(rules, r =>
            r.Type == SelectionRuleType.Implication &&
            r.LegA == "bb_knockdowns_over_1_5" && r.LegB == "bb_knockdowns_over_0_5");
    }

    [Fact]
    public void BuildRules_SkipsRulesForMissingLegs()
    {
        var legs = new[] { "bb_red_win" };

        var rules = _factory.BuildRules(legs);

        Assert.DoesNotContain(rules, r =>
            r.LegA == "bb_red_ko" || r.LegB == "bb_red_ko");
    }
}
