using System.Text.RegularExpressions;

namespace ThePredictions.Application.Services;

/// <summary>
/// Extracts the distinct <c>{{ params.X }}</c> merge-tag names from a Brevo template's HTML,
/// preserving first-seen (document) order so the test-tool form lists inputs in that order.
/// </summary>
public static partial class EmailTemplateParameters
{
    public static IReadOnlyList<string> Extract(string? htmlContent)
    {
        if (string.IsNullOrEmpty(htmlContent))
            return [];

        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in ParamTagRegex().Matches(htmlContent))
        {
            var name = match.Groups[1].Value;
            if (seen.Add(name))
                names.Add(name);
        }

        return names;
    }

    [GeneratedRegex(@"\{\{\s*params\.([A-Za-z0-9_]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ParamTagRegex();
}
