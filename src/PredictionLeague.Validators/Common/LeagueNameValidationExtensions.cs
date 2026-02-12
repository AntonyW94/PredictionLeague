using FluentValidation;
using System.Text.RegularExpressions;

namespace PredictionLeague.Validators.Common;

public static partial class LeagueNameValidationExtensions
{
    // Allow letters (any language), numbers, spaces, and common punctuation
    // Disallow: < > " ' ` \ / to prevent XSS and injection attacks
    [GeneratedRegex(@"^[\p{L}\p{N}\s\-\.\,\!\?\&\(\)\:\;]+$", RegexOptions.Compiled)]
    private static partial Regex SafeLeagueNameRegex();

    /// <summary>
    /// Validates that the league name contains only safe characters.
    /// Allowed: letters (any language), numbers, spaces, hyphens, periods, commas,
    /// exclamation marks, question marks, ampersands, parentheses, colons, semicolons.
    /// </summary>
    public static void MustBeASafeLeagueName<T>(this IRuleBuilder<T, string> ruleBuilder)
    {
        ruleBuilder
            .Must(name => string.IsNullOrEmpty(name) || SafeLeagueNameRegex().IsMatch(name))
            .WithMessage("League name can only contain letters, numbers, spaces, and common punctuation (- . , ! ? & ( ) : ;).");
    }
}
