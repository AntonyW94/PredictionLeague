using FluentValidation;
using PredictionLeague.Contracts.Admin.Teams;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Admin.Teams;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class CreateTeamRequestValidator : AbstractValidator<CreateTeamRequest>
{
    public CreateTeamRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.LogoUrl).NotEmpty().Must(BeAValidUrl).WithMessage("A valid logo URL is required.");
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}