using FluentAssertions;
using FluentValidation.TestHelper;
using ThePredictions.Tests.Builders.Admin.Competitions;
using ThePredictions.Validators.Admin.Competitions;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.Competitions;

public class CreateCompetitionRequestValidatorTests
{
    private readonly CreateCompetitionRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new CreateCompetitionRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeIsEmpty()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("Please enter a competition code.");
    }

    [Fact]
    public void Validate_ShouldNotCheckCodeFormat_WhenCodeIsEmpty()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .Should().ContainSingle();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeIsTooShort()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode("A")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("The competition code must be between 2 and 50 characters.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeIsTooLong()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode(new string('A', 51))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("The competition code must be between 2 and 50 characters.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeContainsLowercaseLetters()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode("prem")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("The competition code may only contain uppercase letters, numbers, and underscores.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenCodeContainsNumbersAndUnderscores()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithCode("UEFA_CL_2026")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Please enter a competition name.");
    }

    [Fact]
    public void Validate_ShouldNotCheckNameFormat_WhenNameIsEmpty()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .Should().ContainSingle();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooShort()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName("A")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("The competition name must be between 2 and 200 characters.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooLong()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName(new string('a', 201))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("The competition name must be between 2 and 200 characters.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameContainsHtmlTags()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName("<b>Premier League</b>")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameContainsAllowedPunctuation()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithName("Premier League (2025-26)")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Validate_ShouldPass_WhenTypeIsLeagueOrTournament(int type)
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithType(type)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Validate_ShouldFail_WhenTypeIsOutsideTheAllowedRange(int type)
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithType(type)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Type)
            .WithErrorMessage("Competition type must be League (0) or Tournament (1).");
    }

    [Fact]
    public void Validate_ShouldFail_WhenLogoUrlIsNotAnAbsoluteUrl()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithLogoUrl("not-a-url")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.LogoUrl)
            .WithErrorMessage("A valid logo URL is required.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_ShouldPass_WhenLogoUrlIsNotSupplied(string? logoUrl)
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithLogoUrl(logoUrl)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.LogoUrl);
    }

    [Fact]
    public void Validate_ShouldFail_WhenApiLeagueIdIsZero()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithApiLeagueId(0)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ApiLeagueId)
            .WithErrorMessage("The API league id must be a positive number.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenApiLeagueIdIsNegative()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithApiLeagueId(-1)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ApiLeagueId);
    }

    [Fact]
    public void Validate_ShouldPass_WhenApiLeagueIdIsNull()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithApiLeagueId(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ApiLeagueId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionIsTooLong()
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithDescription(new string('a', 2001))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("The description must be 2000 characters or fewer.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_ShouldPass_WhenDescriptionIsNotSupplied(string? description)
    {
        var request = new CreateCompetitionRequestBuilder()
            .WithDescription(description)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
