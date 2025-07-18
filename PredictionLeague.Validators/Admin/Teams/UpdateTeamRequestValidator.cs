using FluentValidation;
using PredictionLeague.Contracts.Admin.Teams;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Teams;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateTeamRequestValidator : AbstractValidator<UpdateTeamRequest>
{
    public UpdateTeamRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a team name.")
            .Length(2, 100).WithMessage("The team name must be between 2 and 100 characters.");

        RuleFor(x => x.LogoUrl)
            .NotEmpty().WithMessage("Please provide a URL for the team's logo.")
            .Must(BeAValidUrl).WithMessage("A valid logo URL is required.");
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}