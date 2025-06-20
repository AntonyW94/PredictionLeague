using FluentValidation;
using PredictionLeague.API.Contracts;

namespace PredictionLeague.API.Validators;

public class SubmitPredictionsRequestValidator : AbstractValidator<SubmitPredictionsRequest>
{
    public SubmitPredictionsRequestValidator()
    {
        RuleFor(x => x.GameWeekId).GreaterThan(0);
        RuleFor(x => x.Predictions).NotEmpty().WithMessage("At least one prediction must be submitted.");
        RuleForEach(x => x.Predictions).SetValidator(new PredictionSubmissionValidator());
    }
}