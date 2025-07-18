using FluentValidation;
using PredictionLeague.Contracts.Admin.Results;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Results;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateMatchResultsRequestValidator : AbstractValidator<UpdateMatchResultsRequest>
{
    public UpdateMatchResultsRequestValidator()
    {
        RuleFor(x => x.MatchId).GreaterThan(0).WithMessage("A valid Match ID must be provided.");
        RuleFor(x => x.HomeScore).InclusiveBetween(0, 9).WithMessage("Home score must be between 0 and 9.");
        RuleFor(x => x.AwayScore).InclusiveBetween(0, 9).WithMessage("Away score must be between 0 and 9.");
    }
}