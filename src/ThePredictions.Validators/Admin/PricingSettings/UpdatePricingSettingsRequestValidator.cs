using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Validators.Admin.PricingSettings;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdatePricingSettingsRequestValidator : AbstractValidator<UpdatePricingSettingsRequest>
{
    public UpdatePricingSettingsRequestValidator()
    {
        RuleFor(x => x.BufferRate)
            .InclusiveBetween(0m, 1m).WithMessage("The buffer must be between 0% and 100%.");

        RuleFor(x => x.MinimumFloor)
            .InclusiveBetween(0m, 1000m).WithMessage("The minimum floor must be between £0 and £1,000.");
    }
}
