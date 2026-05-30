using FluentValidation.TestHelper;
using ThePredictions.Tests.Builders.Admin.Seasons;
using ThePredictions.Validators.Admin.Seasons;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.Seasons;

public class CreateSeasonRequestValidatorTests
{
    private readonly CreateSeasonRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new CreateSeasonRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldNotValidateName_WhenNameIsEmpty()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooShort()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName("ABC")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooLong()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName(new string('a', 51))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameContainsHtmlTags()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName("<b>Season</b>")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameContainsAllowedPunctuation()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName("Premier League 2024-25")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldSkipNameFormatRules_WhenNameIsEmpty()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenStartDateUtcIsDefault()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithStartDateUtc(default)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.StartDateUtc);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEndDateUtcIsDefault()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithEndDateUtc(default)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EndDateUtc);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEndDateIsBeforeStartDate()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithStartDateUtc(new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc))
            .WithEndDateUtc(new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EndDateUtc);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNumberOfRoundsIsZero()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithNumberOfRounds(0)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.NumberOfRounds);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNumberOfRoundsExceeds52()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithNumberOfRounds(53)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.NumberOfRounds);
    }

    [Fact]
    public void Validate_ShouldPass_WhenNumberOfRoundsIs1()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithNumberOfRounds(1)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.NumberOfRounds);
    }

    [Fact]
    public void Validate_ShouldPass_WhenNumberOfRoundsIs52()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithNumberOfRounds(52)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.NumberOfRounds);
    }

    [Fact]
    public void Validate_ShouldFail_WhenCompetitionIdIsZero()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithCompetitionId(0)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CompetitionId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenCompetitionIdIsNegative()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithCompetitionId(-1)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CompetitionId);
    }

    [Fact]
    public void Validate_ShouldPass_WhenCompetitionIdIsPositive()
    {
        var request = new CreateSeasonRequestBuilder()
            .WithCompetitionId(1)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.CompetitionId);
    }
}
