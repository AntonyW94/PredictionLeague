using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Validators.Prizes;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class PrizeSchemeRequestValidator : AbstractValidator<PrizeSchemeRequest>
{
    public PrizeSchemeRequestValidator()
    {
        RuleFor(x => x.AdminTopUpPounds)
            .GreaterThanOrEqualTo(0).WithMessage("The admin top-up must be zero or more whole pounds.");

        RuleFor(x => x.OverallFivePoundThreshold)
            .GreaterThanOrEqualTo(0).WithMessage("The £5-rounding threshold must be zero or more.");

        RuleFor(x => x.Categories)
            .NotEmpty().WithMessage("Enable at least one prize category.");

        RuleFor(x => x.Categories)
            .Must(categories => categories.Select(c => c.Category).Distinct().Count() == categories.Count)
            .WithMessage("A prize category cannot be enabled more than once.")
            .When(x => x.Categories.Count != 0);

        RuleForEach(x => x.Categories).ChildRules(category =>
        {
            category.RuleFor(c => c.PerEntryPounds)
                .GreaterThanOrEqualTo(0).WithMessage("Each per-entry allocation must be zero or more whole pounds.");
        });
    }
}
