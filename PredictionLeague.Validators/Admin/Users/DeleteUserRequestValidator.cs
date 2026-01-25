using FluentValidation;
using PredictionLeague.Contracts.Admin.Users;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Users;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class DeleteUserRequestValidator : AbstractValidator<DeleteUserRequest>
{
    public DeleteUserRequestValidator()
    {
        RuleFor(x => x.NewAdministratorId)
            .NotEmpty()
            .When(x => x.NewAdministratorId is not null)
            .WithMessage("New administrator ID cannot be empty when specified.");
    }
}
