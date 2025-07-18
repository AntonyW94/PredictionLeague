using FluentValidation;
using PredictionLeague.Contracts.Leagues;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Validators.Leagues;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class JoinLeagueRequestValidator : AbstractValidator<JoinLeagueRequest>
{
    public JoinLeagueRequestValidator()
    {
        RuleFor(x => x.EntryCode)
            .NotEmpty().WithMessage("Please enter an entry code.")
            .Length(6).WithMessage("The entry code must be 6 characters long.");
    }
}