using FluentValidation;
using PredictionLeague.Shared.Auth;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Validators.Auth;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}