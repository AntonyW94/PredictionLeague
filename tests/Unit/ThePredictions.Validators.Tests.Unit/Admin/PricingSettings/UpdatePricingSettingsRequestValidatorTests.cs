using FluentValidation.TestHelper;
using ThePredictions.Tests.Builders.Admin.PricingSettings;
using ThePredictions.Validators.Admin.PricingSettings;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.PricingSettings;

public class UpdatePricingSettingsRequestValidatorTests
{
    private readonly UpdatePricingSettingsRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new UpdatePricingSettingsRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void Validate_ShouldPass_WhenBufferRateIsWithinRange(decimal bufferRate)
    {
        var request = new UpdatePricingSettingsRequestBuilder()
            .WithBufferRate(bufferRate)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.BufferRate);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Validate_ShouldFail_WhenBufferRateIsOutsideRange(decimal bufferRate)
    {
        var request = new UpdatePricingSettingsRequestBuilder()
            .WithBufferRate(bufferRate)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.BufferRate)
            .WithErrorMessage("The buffer must be between 0% and 100%.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Validate_ShouldPass_WhenMinimumFloorIsWithinRange(decimal minimumFloor)
    {
        var request = new UpdatePricingSettingsRequestBuilder()
            .WithMinimumFloor(minimumFloor)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.MinimumFloor);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1000.01)]
    public void Validate_ShouldFail_WhenMinimumFloorIsOutsideRange(decimal minimumFloor)
    {
        var request = new UpdatePricingSettingsRequestBuilder()
            .WithMinimumFloor(minimumFloor)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MinimumFloor)
            .WithErrorMessage("The minimum floor must be between £0 and £1,000.");
    }
}
