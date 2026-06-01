using FluentValidation;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Validators.Common;

namespace ThePredictions.Validators.Admin.Seasons;

public abstract class BaseSeasonRequestValidator<T> : AbstractValidator<T> where T : BaseSeasonRequest
{
    protected BaseSeasonRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a name for the season.")
            .Length(4, 50).WithMessage("The season name must be between 4 and 50 characters.")
            .MustBeASafeName("Season name")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.StartDateUtc)
            .NotEmpty().WithMessage("Please provide a start date for the season.");

        RuleFor(x => x.EndDateUtc)
            .NotEmpty().WithMessage("Please provide an end date for the season.")
            .GreaterThan(x => x.StartDateUtc).WithMessage("The end date must be after the start date.");
   
        RuleFor(x => x.NumberOfRounds)
            .InclusiveBetween(1, 52).WithMessage("Number of rounds must be between 1 and 52.");

        RuleFor(x => x.CompetitionId)
            .GreaterThan(0).WithMessage("Please select a competition.");

        RuleFor(x => x.PassStandardPrice)
            .InclusiveBetween(0m, 1000m).WithMessage("The Standard price must be between £0 and £1,000.")
            .When(x => x.PassStandardPrice.HasValue);
    }
}