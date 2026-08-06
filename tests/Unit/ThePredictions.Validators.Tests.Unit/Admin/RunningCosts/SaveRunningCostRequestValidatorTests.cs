using FluentValidation.TestHelper;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Builders.Admin.RunningCosts;
using ThePredictions.Validators.Admin.RunningCosts;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Admin.RunningCosts;

public class SaveRunningCostRequestValidatorTests
{
    private static readonly DateTime StartDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SaveRunningCostRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new SaveRunningCostRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Please enter a name for the cost.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsTooLong()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithName(new string('a', 151))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("The name must be 150 characters or fewer.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameIsAtTheMaximumLength()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithName(new string('a', 150))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12.99)]
    [InlineData(1000000)]
    public void Validate_ShouldPass_WhenAmountIsWithinRange(decimal amount)
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithAmount(amount)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAmountIsNegative()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithAmount(-0.01m)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Amount must be 0 or greater.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenAmountExceedsTheMaximum()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithAmount(1_000_000.01m)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount)
            .WithErrorMessage("Amount must not exceed 1,000,000.");
    }

    [Theory]
    [InlineData(CostFrequency.Monthly)]
    [InlineData(CostFrequency.Annual)]
    [InlineData(CostFrequency.OneOff)]
    public void Validate_ShouldPass_WhenFrequencyIsARecognisedValue(CostFrequency frequency)
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithFrequency(frequency)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Frequency);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFrequencyIsNotARecognisedValue()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithFrequency((CostFrequency)99)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Frequency)
            .WithErrorMessage("Select a valid frequency.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenStartDateIsNotSupplied()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithStartDateUtc(default)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.StartDateUtc)
            .WithErrorMessage("Enter the start date.");
    }

    [Fact]
    public void Validate_ShouldFail_WhenEndDateIsBeforeTheStartDate()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithStartDateUtc(StartDateUtc)
            .WithEndDateUtc(StartDateUtc.AddDays(-1))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.EndDateUtc)
            .WithErrorMessage("The end date must be on or after the start date.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenEndDateMatchesTheStartDate()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithStartDateUtc(StartDateUtc)
            .WithEndDateUtc(StartDateUtc)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.EndDateUtc);
    }

    [Fact]
    public void Validate_ShouldPass_WhenEndDateIsAfterTheStartDate()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithStartDateUtc(StartDateUtc)
            .WithEndDateUtc(StartDateUtc.AddYears(1))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.EndDateUtc);
    }

    [Fact]
    public void Validate_ShouldPass_WhenEndDateIsNull()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithEndDateUtc(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.EndDateUtc);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNotesAreTooLong()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithNotes(new string('a', 501))
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes must be 500 characters or fewer.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenNotesAreNull()
    {
        var request = new SaveRunningCostRequestBuilder()
            .WithNotes(null)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }
}
