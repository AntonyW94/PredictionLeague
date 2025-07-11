using FluentValidation;
using PredictionLeague.Shared.Admin.Results;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Results;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateMatchResultsRequestValidator : AbstractValidator<UpdateMatchResultsRequest>
{
    public UpdateMatchResultsRequestValidator()
    {
        RuleFor(x => x.MatchId).GreaterThan(0);
        RuleFor(x => x.HomeScore).InclusiveBetween(0, 9);
        RuleFor(x => x.AwayScore).InclusiveBetween(0, 9);
    }
}