using FluentValidation.TestHelper;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Tests.Builders.Boosts;
using ThePredictions.Validators.Boosts;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Boosts;

public class SetLeagueBoostRulesRequestValidatorTests
{
    private readonly SetLeagueBoostRulesRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new SetLeagueBoostRulesRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldPass_WhenThereAreNoSelections()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldPass_WhenASelectionHasNoWindows()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithWindows([])
                    .Build()
            ])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenBoostCodeIsEmpty()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithBoostCode("")
                    .Build()
            ])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].BoostCode")
            .WithErrorMessage("Each boost must have a code.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenTotalUsesPerSeasonIsNegative()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithTotalUsesPerSeason(-1)
                    .Build()
            ])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].TotalUsesPerSeason")
            .WithErrorMessage("The number of uses per season must be zero or more.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenTotalUsesPerSeasonIsZero()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithTotalUsesPerSeason(0)
                    .Build()
            ])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAWindowStartsBeforeRoundOne()
    {
        var request = BuildRequestWithWindow(new BoostWindowSelectionDtoBuilder()
            .WithStartRoundNumber(0)
            .Build());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].Windows[0].StartRoundNumber")
            .WithErrorMessage("A boost window must start at round 1 or later.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenAWindowEndsBeforeItStarts()
    {
        var request = BuildRequestWithWindow(new BoostWindowSelectionDtoBuilder()
            .WithStartRoundNumber(10)
            .WithEndRoundNumber(9)
            .Build());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].Windows[0].EndRoundNumber")
            .WithErrorMessage("A boost window must end on or after it starts.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenAWindowStartsAndEndsOnTheSameRound()
    {
        var request = BuildRequestWithWindow(new BoostWindowSelectionDtoBuilder()
            .WithStartRoundNumber(5)
            .WithEndRoundNumber(5)
            .Build());

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenMaxUsesInWindowIsNegative()
    {
        var request = BuildRequestWithWindow(new BoostWindowSelectionDtoBuilder()
            .WithMaxUsesInWindow(-1)
            .Build());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].Windows[0].MaxUsesInWindow")
            .WithErrorMessage("The maximum uses in a window must be zero or more.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenMaxUsesInWindowIsZero()
    {
        var request = BuildRequestWithWindow(new BoostWindowSelectionDtoBuilder()
            .WithMaxUsesInWindow(0)
            .Build());

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldReportErrors_ForEverySelectionAndWindow()
    {
        var request = new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithBoostCode("")
                    .Build(),
                new LeagueBoostSelectionDtoBuilder()
                    .WithWindows([
                        new BoostWindowSelectionDtoBuilder().Build(),
                        new BoostWindowSelectionDtoBuilder().WithStartRoundNumber(-1).Build()
                    ])
                    .Build()
            ])
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Selections[0].BoostCode");
        result.ShouldHaveValidationErrorFor("Selections[1].Windows[1].StartRoundNumber");
    }

    private static SetLeagueBoostRulesRequest BuildRequestWithWindow(BoostWindowSelectionDto window) =>
        new SetLeagueBoostRulesRequestBuilder()
            .WithSelections([
                new LeagueBoostSelectionDtoBuilder()
                    .WithWindows([window])
                    .Build()
            ])
            .Build();
}
