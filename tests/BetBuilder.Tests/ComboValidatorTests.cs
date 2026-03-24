using BetBuilder.Application.Validation;
using BetBuilder.Domain;
using BetBuilder.Infrastructure.Rules;
using Microsoft.Extensions.Options;

namespace BetBuilder.Tests;

public class ComboValidatorTests
{
    private static ComboValidator CreateValidator(int maxLegs = 12)
    {
        var ruleFactory = new SelectionRuleFactory();
        var settings = Options.Create(new PricingSettings { MaxLegs = maxLegs });
        return new ComboValidator(ruleFactory, settings);
    }

    [Fact]
    public void Validate_ValidSelection_NoErrors()
    {
        var validator = CreateValidator();
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(new[] { "bb_red_win" }, snapshot);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Validate_UnknownLeg_ReturnsError()
    {
        var validator = CreateValidator();
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(new[] { "bb_nonexistent" }, snapshot);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ValidationIssueCode.UnknownLeg);
    }

    [Fact]
    public void Validate_DuplicateLeg_ReturnsError()
    {
        var validator = CreateValidator();
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(new[] { "bb_red_win", "bb_red_win" }, snapshot);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ValidationIssueCode.DuplicateLeg);
    }

    [Fact]
    public void Validate_MaxLegsExceeded_ReturnsError()
    {
        var validator = CreateValidator(maxLegs: 2);
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(
            new[] { "bb_red_win", "bb_draw", "bb_red_ko" }, snapshot);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ValidationIssueCode.MaxLegsExceeded);
    }

    [Fact]
    public void Validate_MutuallyExclusiveLegs_ReturnsError()
    {
        var validator = CreateValidator();
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(new[] { "bb_red_win", "bb_blue_win" }, snapshot);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ValidationIssueCode.MutuallyExclusive);
    }

    [Fact]
    public void Validate_RedundantSelection_ReturnsWarning()
    {
        var validator = CreateValidator();
        var snapshot = TestHelpers.CreateSnapshot();

        var result = validator.Validate(new[] { "bb_red_ko", "bb_red_win" }, snapshot);

        Assert.False(result.HasErrors);
        Assert.Contains(result.Warnings, w => w.Code == ValidationIssueCode.RedundantSelection);
    }

    [Fact]
    public void Validate_UnavailableLeg_ReturnsError()
    {
        var legs = new[] { "bb_red_win", "bb_never" };
        var rows = new[]
        {
            new byte[] { 1, 0 },
            new byte[] { 0, 0 },
        };
        var snapshot = TestHelpers.CreateSnapshot(legs: legs, outcomeRows: rows);

        var validator = CreateValidator();
        var result = validator.Validate(new[] { "bb_never" }, snapshot);

        Assert.True(result.HasErrors);
        Assert.Contains(result.Errors, e => e.Code == ValidationIssueCode.UnavailableLeg);
    }
}
