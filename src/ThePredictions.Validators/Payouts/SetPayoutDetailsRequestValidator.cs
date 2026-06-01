using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Payouts;

namespace ThePredictions.Validators.Payouts;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class SetPayoutDetailsRequestValidator : AbstractValidator<SetPayoutDetailsRequest>
{
    public SetPayoutDetailsRequestValidator()
    {
        RuleFor(x => x.AccountName)
            .NotEmpty().WithMessage("Enter the account name.")
            .MaximumLength(100).WithMessage("The account name must be 100 characters or fewer.");

        RuleFor(x => x.SortCode)
            .NotEmpty().WithMessage("Enter the sort code.")
            .Matches(@"^\d{2}-?\d{2}-?\d{2}$").WithMessage("Enter a valid sort code, for example 12-34-56.");

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Enter the account number.")
            .Matches(@"^\d{8}$").WithMessage("Enter a valid 8-digit account number.");
    }
}
