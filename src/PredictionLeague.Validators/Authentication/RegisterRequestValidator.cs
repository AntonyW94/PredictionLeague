using FluentValidation;
using PredictionLeague.Contracts.Authentication;
using PredictionLeague.Validators.Common;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Authentication;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Please enter your first name.")
            .Length(2, 50).WithMessage("Your first name must be between 2 and 50 characters.")
            .MustBeASafeName();

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Please enter your last name.")
            .Length(2, 50).WithMessage("Your last name must be between 2 and 50 characters.")
            .MustBeASafeName();

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Please enter your email address.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Please create a password.")
            .Length(8, 100).WithMessage("Your password must be at least 8 characters long.");
    }
}