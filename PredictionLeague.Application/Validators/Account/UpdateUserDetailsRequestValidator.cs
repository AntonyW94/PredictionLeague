using FluentValidation;
using PredictionLeague.Shared.Account;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Validators.Account;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateUserDetailsRequestValidator : AbstractValidator<UpdateUserDetailsRequest>
{
    public UpdateUserDetailsRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .Length(2, 50);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .Length(2, 50);

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^07\d{9}$").WithMessage("Please enter a valid 11-digit UK mobile number starting with 07.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}