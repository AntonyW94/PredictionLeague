using FluentValidation;
using System.Text.RegularExpressions;

namespace PredictionLeague.Validators.Common;

public static partial class SafeNameValidationExtensions
{
    // Allow letters (any language), numbers, spaces, and common punctuation
    // Disallow: < > " ' ` \ / to prevent XSS and injection attacks
    [GeneratedRegex(@"^[\p{L}\p{N}\s\-\.\,\!\?\&\(\)\:\;]+$", RegexOptions.Compiled)]
    private static partial Regex SafeNameRegex();

    /// <summary>
    /// Validates that a name contains only safe characters.
    /// Allowed: letters (any language), numbers, spaces, hyphens, periods, commas,
    /// exclamation marks, question marks, ampersands, parentheses, colons, semicolons.
    /// </summary>
    public static IRuleBuilderOptions<T, string> MustBeASafeName<T>(
        this IRuleBuilder<T, string> ruleBuilder, string fieldDescription = "Name")
    {
        return ruleBuilder
            .Must(name => string.IsNullOrEmpty(name) || SafeNameRegex().IsMatch(name))
            .WithMessage($"{fieldDescription} can only contain letters, numbers, spaces, and common punctuation (- . , ! ? & ( ) : ;).");
    }
}
