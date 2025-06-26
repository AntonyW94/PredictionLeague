using FluentValidation;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.API.Validators
{
    public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
    {
        public CreateLeagueRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("League name cannot be empty.")
                .Length(3, 100).WithMessage("League name must be between 3 and 100 characters.");

            RuleFor(x => x.GameYearId).GreaterThan(0).WithMessage("A valid Game Year must be provided.");
        }
    }
}
