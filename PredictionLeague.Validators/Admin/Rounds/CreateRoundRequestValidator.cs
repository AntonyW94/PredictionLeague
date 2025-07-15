using FluentValidation;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Validators.Admin.Matches;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Rounds;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateRoundRequestValidator : AbstractValidator<CreateRoundRequest>
{
    public CreateRoundRequestValidator()
    {
        RuleFor(x => x.SeasonId).GreaterThan(0);
        RuleFor(x => x.RoundNumber).InclusiveBetween(1, 52);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.Deadline).NotEmpty().GreaterThan(x => x.StartDate);
        RuleFor(x => x.Matches).NotEmpty().WithMessage("A round must contain at least one match.");
        RuleForEach(x => x.Matches).SetValidator(new CreateMatchRequestValidator());
    }
}