using FluentValidation;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Validators.Admin.Teams;

public abstract class BaseTeamRequestValidator<T> : AbstractValidator<T> where T : BaseTeamRequest
{
    protected BaseTeamRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a team name.");
 
        RuleFor(x => x.Name)
            .Length(2, 100).WithMessage("The team name must be between 2 and 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.LogoUrl)
            .NotEmpty().WithMessage("Please provide a URL for the team's logo.");
        
        RuleFor(x => x.LogoUrl)
            .Must(BeAValidUrl).WithMessage("A valid logo URL is required.")
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}