using FluentValidation;
using PredictionLeague.Application.Validators.Admin.Matches;
using PredictionLeague.Shared.Admin.Rounds;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Validators.Admin.Rounds;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateRoundRequestValidator : AbstractValidator<UpdateRoundRequest>
{
    public UpdateRoundRequestValidator()
    {
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.Deadline).NotEmpty().GreaterThan(x => x.StartDate);
        RuleFor(x => x.Matches).NotEmpty().WithMessage("A round must contain at least one match.");
        RuleForEach(x => x.Matches).SetValidator(new UpdateMatchRequestValidator());
    }
}