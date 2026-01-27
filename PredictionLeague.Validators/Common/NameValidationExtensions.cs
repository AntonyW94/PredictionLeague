using FluentValidation;
using PredictionLeague.Domain.Common.Validation;

namespace PredictionLeague.Validators.Common;

public static class NameValidationExtensions
{
    /// <summary>
    /// Validates that the name contains only safe characters.
    /// Allowed: letters (any language), spaces, hyphens, apostrophes, and periods.
    /// </summary>
    public static void MustBeASafeName<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        ruleBuilder
            .Must(NameValidator.IsValid)
            .WithMessage("Name can only contain letters, spaces, hyphens, apostrophes, and periods.");
    }
}
