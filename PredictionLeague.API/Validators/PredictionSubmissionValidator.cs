using FluentValidation;
using PredictionLeague.API.Contracts;

namespace PredictionLeague.API.Validators;

public class PredictionSubmissionValidator : AbstractValidator<PredictionSubmission>
{
    public PredictionSubmissionValidator()
    {
        RuleFor(x => x.MatchId).GreaterThan(0);
        RuleFor(x => x.PredictedHomeScore).InclusiveBetween(0, 99);
        RuleFor(x => x.PredictedAwayScore).InclusiveBetween(0, 99);
    }
}