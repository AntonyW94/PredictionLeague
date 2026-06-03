using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Validators.Boosts;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class SetLeagueBoostRulesRequestValidator : AbstractValidator<SetLeagueBoostRulesRequest>
{
    public SetLeagueBoostRulesRequestValidator()
    {
        RuleForEach(x => x.Selections).ChildRules(selection =>
        {
            selection.RuleFor(s => s.BoostCode)
                .NotEmpty().WithMessage("Each boost must have a code.");

            selection.RuleFor(s => s.TotalUsesPerSeason)
                .GreaterThanOrEqualTo(0).WithMessage("The number of uses per season must be zero or more.");

            selection.RuleForEach(s => s.Windows).ChildRules(window =>
            {
                window.RuleFor(w => w.StartRoundNumber)
                    .GreaterThan(0).WithMessage("A boost window must start at round 1 or later.");

                window.RuleFor(w => w.EndRoundNumber)
                    .GreaterThanOrEqualTo(w => w.StartRoundNumber).WithMessage("A boost window must end on or after it starts.");

                window.RuleFor(w => w.MaxUsesInWindow)
                    .GreaterThanOrEqualTo(0).WithMessage("The maximum uses in a window must be zero or more.");
            });
        });
    }
}
