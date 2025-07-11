using FluentValidation;
using PredictionLeague.Shared.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("League name cannot be empty.")
            .Length(3, 100).WithMessage("League name must be between 3 and 100 characters.");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("A valid Season must be provided.");
    }
}