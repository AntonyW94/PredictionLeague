using FluentValidation.TestHelper;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Validators.Payouts;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Payouts;

public class SetPayoutDetailsRequestValidatorTests
{
    private readonly SetPayoutDetailsRequestValidator _validator = new();

    private static SetPayoutDetailsRequest Valid() => new()
    {
        AccountName = "Mr A Willson",
        SortCode = "12-34-56",
        AccountNumber = "12345678"
    };

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsValid()
    {
        var result = _validator.TestValidate(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAccountNameEmpty()
    {
        var request = Valid();
        request.AccountName = "";

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.AccountName);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12-34-5")]
    [InlineData("abcdef")]
    public void Validate_ShouldFail_WhenSortCodeInvalid(string sortCode)
    {
        var request = Valid();
        request.SortCode = sortCode;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.SortCode);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("1234567a")]
    public void Validate_ShouldFail_WhenAccountNumberInvalid(string accountNumber)
    {
        var request = Valid();
        request.AccountNumber = accountNumber;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.AccountNumber);
    }

    [Fact]
    public void Validate_ShouldPass_WhenSortCodeHasNoDashes()
    {
        var request = Valid();
        request.SortCode = "123456";

        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.SortCode);
    }
}
