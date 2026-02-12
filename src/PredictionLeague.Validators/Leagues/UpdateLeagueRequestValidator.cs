using FluentValidation;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Validators.Common;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateLeagueRequestValidator : AbstractValidator<UpdateLeagueRequest>
{
    public UpdateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("League name cannot be empty.")
            .Length(3, 100).WithMessage("League name must be between 3 and 100 characters.")
            .MustBeASafeLeagueName();

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be 0 or greater.")
            .LessThanOrEqualTo(10000).WithMessage("Price must not exceed 10,000.");

        RuleFor(x => x.EntryDeadlineUtc)
            .GreaterThan(DateTime.UtcNow).WithMessage("Entry deadline must be in the future.");

        RuleFor(x => x.PointsForExactScore)
            .InclusiveBetween(1, 100).WithMessage("Points for exact score must be between 1 and 100.");

        RuleFor(x => x.PointsForCorrectResult)
            .InclusiveBetween(1, 100).WithMessage("Points for correct result must be between 1 and 100.");
    }
}