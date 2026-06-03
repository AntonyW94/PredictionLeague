using FluentValidation;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Validators.Common;
using ThePredictions.Validators.Prizes;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a name for your league.")
            .Length(3, 100).WithMessage("The league name must be between 3 and 100 characters.")
            .MustBeASafeLeagueName();

        RuleFor(x => x.SeasonId)
            .GreaterThan(0).WithMessage("You must select a valid season for the league.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be 0 or greater.")
            .LessThanOrEqualTo(10000).WithMessage("Price must not exceed 10,000.");

        RuleFor(x => x.EntryDeadlineUtc)
            .GreaterThan(DateTime.UtcNow).WithMessage("Entry deadline must be in the future.");

        RuleFor(x => x.PointsForExactScore)
            .InclusiveBetween(1, 100).WithMessage("Points for exact score must be between 1 and 100.");

        RuleFor(x => x.PointsForCorrectResult)
            .InclusiveBetween(1, 100).WithMessage("Points for correct result must be between 1 and 100.");

        this.AddBankDetailRules();

        When(x => x.PrizeScheme is not null, () =>
        {
            RuleFor(x => x.Price)
                .Must(price => price == decimal.Truncate(price)).WithMessage("The entry fee must be a whole number of pounds when prizes are enabled.");

            RuleFor(x => x.PrizeScheme!)
                .SetValidator(new PrizeSchemeRequestValidator());
        });
    }
}