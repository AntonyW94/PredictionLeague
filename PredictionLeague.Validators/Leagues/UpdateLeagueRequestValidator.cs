using FluentValidation;
using PredictionLeague.Contracts.Admin.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateLeagueRequestValidator : AbstractValidator<UpdateLeagueRequest>
{
    public UpdateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("League name cannot be empty.")
            .Length(3, 100).WithMessage("League name must be between 3 and 100 characters.");

        RuleFor(x => x.EntryCode)
            .Length(6).WithMessage("Entry code must be 6 characters long.")
            .When(x => !string.IsNullOrEmpty(x.EntryCode));
    }
}