using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Validators.Admin.RunningCosts;

[SuppressMessage("ReSharper", "UnusedType.Global")]
public class SaveRunningCostRequestValidator : AbstractValidator<SaveRunningCostRequest>
{
    public SaveRunningCostRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Please enter a name for the cost.")
            .MaximumLength(150).WithMessage("The name must be 150 characters or fewer.");

        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Amount must be 0 or greater.")
            .LessThanOrEqualTo(1_000_000).WithMessage("Amount must not exceed 1,000,000.");

        RuleFor(x => x.Frequency)
            .IsInEnum().WithMessage("Select a valid frequency.");

        RuleFor(x => x.StartDateUtc)
            .NotEmpty().WithMessage("Enter the start date.");

        RuleFor(x => x.EndDateUtc)
            .GreaterThanOrEqualTo(x => x.StartDateUtc).WithMessage("The end date must be on or after the start date.")
            .When(x => x.EndDateUtc.HasValue);

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
    }
}
