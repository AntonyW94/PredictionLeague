using FluentValidation;
using PredictionLeague.Contracts.Authentication;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Authentication;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Please enter a new password.")
            .MinimumLength(8).WithMessage("Your password must be at least 8 characters long.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password.")
            .Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
