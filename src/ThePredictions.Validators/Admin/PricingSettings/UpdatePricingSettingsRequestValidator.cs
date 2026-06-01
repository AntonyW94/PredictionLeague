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

        RuleFor(x => x.StripePercent)
            .InclusiveBetween(0m, 0.99m).WithMessage("The Stripe percentage must be between 0% and 99%.");

        RuleFor(x => x.StripeFixedFee)
            .InclusiveBetween(0m, 10m).WithMessage("The Stripe fixed fee must be between £0 and £10.");

        RuleFor(x => x.MinimumFloor)
            .InclusiveBetween(0m, 1000m).WithMessage("The minimum floor must be between £0 and £1,000.");
    }
}
