using FluentValidation;
using ThePredictions.Contracts.Admin.Competitions;
using ThePredictions.Validators.Common;

namespace ThePredictions.Validators.Admin.Competitions;

public abstract class BaseCompetitionRequestValidator<T> : AbstractValidator<T> where T : BaseCompetitionRequest
{
    protected BaseCompetitionRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Please enter a competition code.");

        RuleFor(x => x.Code)
            .Length(2, 50).WithMessage("The competition code must be between 2 and 50 characters.")
            .Matches("^[A-Z0-9_]+$").WithMessage("The competition code may only contain uppercase letters, numbers, and underscores.")
            .When(x => !string.IsNullOrEmpty(x.Code));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a competition name.");

        RuleFor(x => x.Name)
            .Length(2, 200).WithMessage("The competition name must be between 2 and 200 characters.")
            .MustBeASafeName("Competition name")
            .When(x => !string.IsNullOrEmpty(x.Name));

        RuleFor(x => x.Type)
            .InclusiveBetween(0, 1).WithMessage("Competition type must be League (0) or Tournament (1).");

        RuleFor(x => x.LogoUrl)
            .Must(BeAValidUrl).WithMessage("A valid logo URL is required.")
            .When(x => !string.IsNullOrEmpty(x.LogoUrl));

        RuleFor(x => x.ApiLeagueId)
            .GreaterThan(0).WithMessage("The API league id must be a positive number.")
            .When(x => x.ApiLeagueId.HasValue);

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("The description must be 2000 characters or fewer.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }

    private static bool BeAValidUrl(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
