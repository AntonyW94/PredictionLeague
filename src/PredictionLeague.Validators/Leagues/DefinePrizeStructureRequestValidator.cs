using FluentValidation;
using PredictionLeague.Contracts.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class DefinePrizeStructureRequestValidator : AbstractValidator<DefinePrizeStructureRequest>
{
    public DefinePrizeStructureRequestValidator()
    {
        RuleForEach(x => x.PrizeSettings)
            .SetValidator(new DefinePrizeSettingDtoValidator());
    }

    private class DefinePrizeSettingDtoValidator : AbstractValidator<DefinePrizeSettingDto>
    {
        public DefinePrizeSettingDtoValidator()
        {
            RuleFor(x => x.PrizeType)
                .IsInEnum()
                .WithMessage("Prize type must be a valid value.");

            RuleFor(x => x.PrizeAmount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Prize amount must be 0 or greater.");

            RuleFor(x => x.Rank)
                .GreaterThan(0)
                .WithMessage("Rank must be greater than 0.");

            RuleFor(x => x.Multiplier)
                .GreaterThan(0)
                .WithMessage("Multiplier must be greater than 0.");

            RuleFor(x => x.PrizeDescription)
                .MaximumLength(200)
                .When(x => !string.IsNullOrEmpty(x.PrizeDescription))
                .WithMessage("Prize description must not exceed 200 characters.");
        }
    }
}
