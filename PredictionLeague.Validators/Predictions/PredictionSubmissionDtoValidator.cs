using FluentValidation;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Validators.Predictions;

public class PredictionSubmissionDtoValidator : AbstractValidator<PredictionSubmissionDto>
{
    public PredictionSubmissionDtoValidator()
    {
        RuleFor(x => x.MatchId).GreaterThan(0);
        RuleFor(x => x.PredictedHomeScore).InclusiveBetween(0, 9);
        RuleFor(x => x.PredictedAwayScore).InclusiveBetween(0, 9);
    }
}