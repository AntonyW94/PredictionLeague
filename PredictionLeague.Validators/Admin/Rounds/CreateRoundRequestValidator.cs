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
        RuleFor(x => x.SeasonId).GreaterThan(0).WithMessage("A valid Season ID must be provided.");
        RuleFor(x => x.RoundNumber).InclusiveBetween(1, 52).WithMessage("Round number must be between 1 and 52.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Please provide a start date.");
        RuleFor(x => x.Deadline).NotEmpty().GreaterThan(x => x.StartDate).WithMessage("The deadline must be after the start date");
        RuleFor(x => x.Matches).NotEmpty().WithMessage("A round must contain at least one match.");
        RuleForEach(x => x.Matches).SetValidator(new CreateMatchRequestValidator());
    }
}