using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using PredictionLeague.Shared.Admin.Leagues;

namespace PredictionLeague.Application.Validators.Admin.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateLeagueRequestValidator : AbstractValidator<UpdateLeagueRequest>
{
    public UpdateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Length(3, 100);

        RuleFor(x => x.EntryCode)
            .Length(6).WithMessage("Entry code must be 6 characters long.")
            .When(x => !string.IsNullOrEmpty(x.EntryCode));
    }
}