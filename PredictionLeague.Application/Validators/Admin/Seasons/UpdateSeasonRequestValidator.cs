using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using PredictionLeague.Shared.Admin.Seasons;

namespace PredictionLeague.Application.Validators.Admin.Seasons;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class UpdateSeasonRequestValidator : AbstractValidator<UpdateSeasonRequest>
{
    public UpdateSeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(4, 50);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThan(x => x.StartDate);
    }
}