using FluentValidation;
using PredictionLeague.Contracts.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a name for your league.")
            .Length(3, 100).WithMessage("The league name must be between 3 and 100 characters.");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("You must select a valid season for the league.");
    }
}