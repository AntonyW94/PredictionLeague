using FluentValidation;
using PredictionLeague.Contracts.Admin.Results;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Rounds;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class MatchResultDtoValidator : AbstractValidator<MatchResultDto>
{
    public MatchResultDtoValidator()
    {
        RuleFor(x => x.MatchId)
            .GreaterThan(0)
            .WithMessage("Match ID must be greater than 0.");

        RuleFor(x => x.HomeScore)
            .InclusiveBetween(0, 9)
            .WithMessage("Home score must be between 0 and 9.");

        RuleFor(x => x.AwayScore)
            .InclusiveBetween(0, 9)
            .WithMessage("Away score must be between 0 and 9.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("Match status must be a valid status value.");
    }
}
