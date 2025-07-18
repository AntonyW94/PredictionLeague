using FluentValidation;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Validators.Predictions;

public class PredictionSubmissionDtoValidator : AbstractValidator<PredictionSubmissionDto>
{
    public PredictionSubmissionDtoValidator()
    {
        RuleFor(x => x.MatchId)
            .GreaterThan(0).WithMessage("A valid Match ID must be provided for each prediction.");

        RuleFor(x => x.PredictedHomeScore)
            .InclusiveBetween(0, 9).WithMessage("Predicted home score must be between 0 and 9.");

        RuleFor(x => x.PredictedAwayScore)
            .InclusiveBetween(0, 9).WithMessage("Predicted away score must be between 0 and 9.");
    }
}