using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Validators.Admin.ServiceFees;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateServiceFeeRequestValidator : AbstractValidator<UpdateServiceFeeRequest>
{
    public UpdateServiceFeeRequestValidator()
    {
        RuleFor(x => x.PercentFee)
            .InclusiveBetween(0m, 0.99m).WithMessage("The percentage fee must be between 0% and 99%.");

        RuleFor(x => x.FixedFee)
            .InclusiveBetween(0m, 10m).WithMessage("The fixed fee must be between £0 and £10.");
    }
}
