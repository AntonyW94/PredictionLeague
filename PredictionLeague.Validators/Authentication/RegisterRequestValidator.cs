using FluentValidation;
using PredictionLeague.Contracts.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Authentication;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().Length(2, 50);
        RuleFor(x => x.LastName).NotEmpty().Length(2, 50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().Length(8, 100);
    }
}