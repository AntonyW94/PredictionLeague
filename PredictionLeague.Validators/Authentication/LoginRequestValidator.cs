using FluentValidation;
using PredictionLeague.Contracts.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Authentication;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Please enter your email address.");
        
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Please enter a valid email address.").When(x => !string.IsNullOrEmpty(x.Email));
        
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Please enter your password.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.");
    }
}