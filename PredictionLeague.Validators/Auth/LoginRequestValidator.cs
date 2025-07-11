using FluentValidation;
using PredictionLeague.Shared.Auth;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Auth;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Please enter your email address.");
        RuleFor(x => x.Email).EmailAddress().WithMessage("Please enter a valid email address.").When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password).NotEmpty().WithMessage("Please enter your password.");
    }
}