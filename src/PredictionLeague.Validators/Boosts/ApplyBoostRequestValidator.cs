using FluentValidation;
using PredictionLeague.Contracts.Boosts;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Boosts;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class ApplyBoostRequestValidator : AbstractValidator<ApplyBoostRequest>
{
    public ApplyBoostRequestValidator()
    {
        RuleFor(x => x.LeagueId)
            .GreaterThan(0)
            .WithMessage("League ID must be greater than 0.");

        RuleFor(x => x.RoundId)
            .GreaterThan(0)
            .WithMessage("Round ID must be greater than 0.");

        RuleFor(x => x.BoostCode)
            .NotEmpty()
            .WithMessage("Boost code is required.")
            .MaximumLength(50)
            .WithMessage("Boost code must not exceed 50 characters.");
    }
}
