using FluentValidation.TestHelper;
using ThePredictions.Tests.Builders.Admin.ServiceFees;
using ThePredictions.Validators.Admin.ServiceFees;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.ServiceFees;

public class UpdateServiceFeeRequestValidatorTests
{
    private readonly UpdateServiceFeeRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new UpdateServiceFeeRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.015)]
    [InlineData(0.99)]
    public void Validate_ShouldPass_WhenPercentFeeIsWithinRange(decimal percentFee)
    {
        var request = new UpdateServiceFeeRequestBuilder()
            .WithPercentFee(percentFee)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.PercentFee);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1)]
    public void Validate_ShouldFail_WhenPercentFeeIsOutsideRange(decimal percentFee)
    {
        var request = new UpdateServiceFeeRequestBuilder()
            .WithPercentFee(percentFee)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PercentFee)
            .WithErrorMessage("The percentage fee must be between 0% and 99%.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    public void Validate_ShouldPass_WhenFixedFeeIsWithinRange(decimal fixedFee)
    {
        var request = new UpdateServiceFeeRequestBuilder()
            .WithFixedFee(fixedFee)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.FixedFee);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10.01)]
    public void Validate_ShouldFail_WhenFixedFeeIsOutsideRange(decimal fixedFee)
    {
        var request = new UpdateServiceFeeRequestBuilder()
            .WithFixedFee(fixedFee)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FixedFee)
            .WithErrorMessage("The fixed fee must be between £0 and £10.");
    }
}
