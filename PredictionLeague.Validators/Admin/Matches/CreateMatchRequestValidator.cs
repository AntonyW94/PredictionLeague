using FluentValidation;
using PredictionLeague.Contracts.Admin.Matches;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Matches;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateMatchRequestValidator : AbstractValidator<CreateMatchRequest>
{
    public CreateMatchRequestValidator()
    {
        RuleFor(x => x.HomeTeamId).GreaterThan(0).WithMessage("A valid home team must be selected.");
        RuleFor(x => x.AwayTeamId).GreaterThan(0).WithMessage("A valid away team must be selected.");
        RuleFor(x => x.AwayTeamId).NotEqual(x => x.HomeTeamId).WithMessage("Home and away teams cannot be the same.");
        RuleFor(x => x.MatchDateTime).NotEmpty().WithMessage("A match date and time must be provided.");
    }
}