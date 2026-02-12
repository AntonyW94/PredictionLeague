using System.Text.RegularExpressions;

namespace PredictionLeague.Domain.Common.Validation;

/// <summary>
/// Validates and sanitizes users names to prevent XSS attacks and ensure data quality.
/// </summary>
public static partial class NameValidator
{
    /// <summary>
    /// Validates that a name contains only safe characters.
    /// Allowed: letters (any language), combining marks, apostrophes, hyphens, spaces, periods.
    /// Blocked: numbers, emojis, HTML characters (&lt; &gt; &amp; etc.), special symbols.
    /// </summary>
    public static bool IsValid(string? name)
    {
        return string.IsNullOrWhiteSpace(name) || SafeNamePattern().IsMatch(name);
    }

    /// <summary>
    /// Removes any characters that don't match the safe name pattern.
    /// Used for external providers (e.g., Google OAuth) where we can't reject invalid input.
    /// </summary>
    public static string Sanitize(string? name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : UnsafeCharactersPattern().Replace(name, "").Trim();
    }

    // Allowed characters:
    // \p{L}  - Any letter (A-Z, a-z, and unicode: e.g. e, n, u, Chinese, Arabic, etc.)
    // \p{M}  - Combining marks (accents, diacritics)
    // '      - Apostrophe (O'Brien, D'Angelo)
    // -      - Hyphen (Mary-Jane, Jean-Pierre)
    // \s     - Whitespace (spaces between names)
    // .      - Period (Jr., Sr., Dr.)
    [GeneratedRegex(@"^[\p{L}\p{M}'\-\s\.]+$", RegexOptions.Compiled)]
    private static partial Regex SafeNamePattern();

    [GeneratedRegex(@"[^\p{L}\p{M}'\-\s\.]", RegexOptions.Compiled)]
    private static partial Regex UnsafeCharactersPattern();
}
