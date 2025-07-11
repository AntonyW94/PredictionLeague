using FluentValidation;
using PredictionLeague.Shared.Predictions;

namespace PredictionLeague.Validators.Predictions;

public class PredictionSubmissionValidator : AbstractValidator<PredictionSubmission>
{
    public PredictionSubmissionValidator()
    {
        RuleFor(x => x.MatchId).GreaterThan(0);
        RuleFor(x => x.PredictedHomeScore).InclusiveBetween(0, 9);
        RuleFor(x => x.PredictedAwayScore).InclusiveBetween(0, 9);
    }
}