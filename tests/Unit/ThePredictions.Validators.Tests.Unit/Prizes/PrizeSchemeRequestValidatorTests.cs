using FluentValidation.TestHelper;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Validators.Prizes;
using Xunit;

namespace ThePredictions.Validators.Tests.Unit.Prizes;

public class PrizeSchemeRequestValidatorTests
{
    private readonly PrizeSchemeRequestValidator _validator = new();

    private static PrizeSchemeRequest Valid() => new()
    {
        AdminTopUpPounds = 0,
        OverallFivePoundThreshold = 100,
        Categories = new List<PrizeSchemeCategoryRequest>
        {
            new() { Category = PrizeType.Overall, PerEntryPounds = 8 },
            new() { Category = PrizeType.Round, PerEntryPounds = 2 }
        }
    };

    [Fact]
    public void Validate_ShouldPass_WhenValid()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNoCategories()
    {
        var request = Valid();
        request.Categories.Clear();

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Categories);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDuplicateCategory()
    {
        var request = Valid();
        request.Categories.Add(new PrizeSchemeCategoryRequest { Category = PrizeType.Overall, PerEntryPounds = 1 });

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Categories);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAdminTopUpNegative()
    {
        var request = Valid();
        request.AdminTopUpPounds = -5;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.AdminTopUpPounds);
    }

    [Fact]
    public void Validate_ShouldFail_WhenThresholdNegative()
    {
        var request = Valid();
        request.OverallFivePoundThreshold = -1;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.OverallFivePoundThreshold);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPerEntryPoundsNegative()
    {
        var request = Valid();
        request.Categories[0].PerEntryPounds = -1;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor("Categories[0].PerEntryPounds");
    }
}
