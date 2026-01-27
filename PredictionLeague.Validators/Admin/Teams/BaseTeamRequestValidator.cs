using FluentValidation;
using PredictionLeague.Contracts.Admin.Teams;
using PredictionLeague.Validators.Common;

namespace PredictionLeague.Validators.Admin.Teams;

public abstract class BaseTeamRequestValidator<T> : AbstractValidator<T> where T : BaseTeamRequest
{
    protected BaseTeamRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a team name.");

        RuleFor(x => x.Name)
            .Length(2, 100).WithMessage("The team name must be between 2 and 100 characters.")
            .MustBeASafeName("Team name")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.ShortName)
            .NotEmpty().WithMessage("Please enter a short name for the team.");

        RuleFor(x => x.ShortName)
            .Length(2, 50).WithMessage("The short name must be between 2 and 50 characters.")
            .MustBeASafeName("Short name")
            .When(x => !string.IsNullOrEmpty(x.ShortName));

        RuleFor(x => x.LogoUrl)
            .NotEmpty().WithMessage("Please provide a URL for the team's logo.");

        RuleFor(x => x.LogoUrl)
            .Must(BeAValidUrl).WithMessage("A valid logo URL is required.")
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));

        RuleFor(x => x.Abbreviation)
            .NotEmpty().WithMessage("Please provide a 3-letter abbreviation.");

        RuleFor(x => x.Abbreviation)
            .Length(3).WithMessage("The abbreviation must be exactly 3 characters.")
            .When(x => !string.IsNullOrEmpty(x.Abbreviation));
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}