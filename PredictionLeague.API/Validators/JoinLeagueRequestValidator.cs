using FluentValidation;
using PredictionLeague.Shared.Leagues;

namespace PredictionLeague.API.Validators;

public class JoinLeagueRequestValidator : AbstractValidator<JoinLeagueRequest>
{
    public JoinLeagueRequestValidator()
    {
        RuleFor(x => x.EntryCode)
            .NotEmpty().WithMessage("Entry code is required.")
            .Length(6).WithMessage("Entry code must be 6 characters long.");
    }
}