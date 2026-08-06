using FluentValidation.TestHelper;
using ThePredictions.Tests.Builders.Admin.Competitions;
using ThePredictions.Validators.Admin.Competitions;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.Competitions;

public class UpdateCompetitionRequestValidatorTests
{
    private readonly UpdateCompetitionRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new UpdateCompetitionRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeIsEmpty()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithCode("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage("Please enter a competition code.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeIsTooShort()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithCode("A")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Validate_ShouldFail_WhenCodeContainsLowercaseLetters()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithCode("prem")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Please enter a competition name.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooLong()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithName(new string('a', 201))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameContainsHtmlTags()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithName("<script>alert(1)</script>")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTypeIsOutsideTheAllowedRange()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithType(2)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLogoUrlIsNotAnAbsoluteUrl()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithLogoUrl("not-a-url")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.LogoUrl);
    }

    [Fact]
    public void Validate_ShouldPass_WhenLogoUrlIsNull()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithLogoUrl(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.LogoUrl);
    }

    [Fact]
    public void Validate_ShouldFail_WhenApiLeagueIdIsZero()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithApiLeagueId(0)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ApiLeagueId);
    }

    [Fact]
    public void Validate_ShouldPass_WhenApiLeagueIdIsNull()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithApiLeagueId(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ApiLeagueId);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionIsTooLong()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithDescription(new string('a', 2001))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_ShouldPass_WhenDescriptionIsNull()
    {
        var request = new UpdateCompetitionRequestBuilder()
            .WithDescription(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }
}
