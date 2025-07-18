using FluentValidation;
using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Validators.Admin.Matches;

public abstract class BaseMatchRequestValidator<T> : AbstractValidator<T> where T : BaseMatchRequest
{
    protected BaseMatchRequestValidator()
    {
        RuleFor(x => x.HomeTeamId)
            .GreaterThan(0).WithMessage("A valid home team must be selected.");

        RuleFor(x => x.AwayTeamId)
            .GreaterThan(0).WithMessage("A valid away team must be selected.")
            .NotEqual(x => x.HomeTeamId).WithMessage("Home and away teams cannot be the same.");

        RuleFor(x => x.MatchDateTime)
            .NotEmpty().WithMessage("A match date and time must be provided.");
    }
}