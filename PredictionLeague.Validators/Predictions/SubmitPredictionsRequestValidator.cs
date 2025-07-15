using FluentValidation;
using PredictionLeague.Contracts.Predictions;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Predictions;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class SubmitPredictionsRequestValidator : AbstractValidator<SubmitPredictionsRequest>
{
    public SubmitPredictionsRequestValidator()
    {
        RuleFor(x => x.RoundId).GreaterThan(0);
        RuleFor(x => x.Predictions).NotEmpty().WithMessage("At least one prediction must be submitted.");
        RuleForEach(x => x.Predictions).SetValidator(new PredictionSubmissionDtoValidator());
    }
}