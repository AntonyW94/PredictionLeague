using FluentValidation;
using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Users;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.NewRole)
            .NotEmpty()
            .WithMessage("Role is required.")
            .Must(role => Enum.TryParse<ApplicationUserRole>(role, ignoreCase: true, out _))
            .WithMessage($"Role must be one of: {string.Join(", ", Enum.GetNames<ApplicationUserRole>())}");
    }
}
