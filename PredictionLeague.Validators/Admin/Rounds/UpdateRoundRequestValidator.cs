using FluentValidation;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Validators.Admin.Matches;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Rounds;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateRoundRequestValidator : AbstractValidator<UpdateRoundRequest>
{
    public UpdateRoundRequestValidator()
    {
        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage("Please provide a start date for the round.");

        RuleFor(x => x.Deadline)
            .NotEmpty().WithMessage("Please provide a prediction deadline.")
            .GreaterThan(x => x.StartDate).WithMessage("The prediction deadline must be after the round's start date.");

        RuleFor(x => x.Matches)
            .NotEmpty().WithMessage("A round must contain at least one match.");

        RuleForEach(x => x.Matches).SetValidator(new UpdateMatchRequestValidator());
    }
}