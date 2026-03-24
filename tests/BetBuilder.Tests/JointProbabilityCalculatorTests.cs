using BetBuilder.Application.Pricing;

namespace BetBuilder.Tests;

public class JointProbabilityCalculatorTests
{
    private readonly JointProbabilityCalculator _calculator = new();

    [Fact]
    public void Calculate_SingleLeg_ReturnsCorrectProbability()
    {
        // 7 out of 10 rows have bb_red_win=1
        var snapshot = TestHelpers.CreateSnapshot();
        var indices = new[] { 0 }; // bb_red_win

        var result = _calculator.Calculate(snapshot, indices);

        Assert.Equal(10, result.TotalScenarios);
        Assert.Equal(7, result.MatchingScenarios);
        Assert.Equal(0.7, result.JointProbability, 6);
    }

    [Fact]
    public void Calculate_TwoCorrelatedLegs_ReturnsCorrectJointProbability()
    {
        // bb_red_win AND bb_red_ko: rows where both are 1
        var snapshot = TestHelpers.CreateSnapshot();
        var indices = new[] { 0, 3 }; // bb_red_win, bb_red_ko

        var result = _calculator.Calculate(snapshot, indices);

        Assert.Equal(10, result.TotalScenarios);
        Assert.Equal(4, result.MatchingScenarios);
        Assert.Equal(0.4, result.JointProbability, 6);
    }

    [Fact]
    public void Calculate_MutuallyExclusiveLegs_ReturnsZero()
    {
        // bb_red_win AND bb_blue_win: never both 1 in the test data
        var snapshot = TestHelpers.CreateSnapshot();
        var indices = new[] { 0, 1 }; // bb_red_win, bb_blue_win

        var result = _calculator.Calculate(snapshot, indices);

        Assert.Equal(0, result.MatchingScenarios);
        Assert.Equal(0.0, result.JointProbability);
    }

    [Fact]
    public void Calculate_AllLegs_ScansCorrectly()
    {
        // Test with all 5 legs: no row has all 5 as 1
        var snapshot = TestHelpers.CreateSnapshot();
        var indices = new[] { 0, 1, 2, 3, 4 };

        var result = _calculator.Calculate(snapshot, indices);

        Assert.Equal(0, result.MatchingScenarios);
    }

    [Fact]
    public void Calculate_ThreeLegs_ReturnsCorrectCount()
    {
        // bb_red_win + bb_red_ko + bb_draw: impossible (red_win and draw exclusive)
        var snapshot = TestHelpers.CreateSnapshot();
        var indices = new[] { 0, 2, 3 }; // red_win, draw, red_ko

        var result = _calculator.Calculate(snapshot, indices);

        Assert.Equal(0, result.MatchingScenarios);
    }
}
