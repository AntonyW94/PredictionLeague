namespace ThePredictions.Application.Services;

public class EmailTestDefaultsResolver : IEmailTestDefaultsResolver
{
    public IReadOnlyDictionary<string, string> Resolve(IReadOnlyList<string> paramNames, EmailTestUserData user, string baseUrl)
    {
        var trimmedBase = baseUrl.TrimEnd('/');

        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in paramNames)
            defaults[name] = ResolveOne(name, user, trimmedBase);

        return defaults;
    }

    private static string ResolveOne(string name, EmailTestUserData user, string baseUrl)
    {
        switch (name.ToUpperInvariant())
        {
            case "FIRST_NAME":
            case "ADMIN_NAME":
                return user.FirstName;
            case "LAST_NAME":
                return user.LastName;
            case "NAME":
            case "FULL_NAME":
                return $"{user.FirstName} {user.LastName}".Trim();
            case "EMAIL":
                return user.Email;
            case "LEAGUE_NAME":
                return "Test League";
            case "SEASON_NAME":
                return "Test Season 2026";
            case "ROUND_NAME":
                return "Round 1";
            case "NEXT_ROUND_NAME":
                return "Round 2";
            case "DEADLINE":
            case "NEXT_ROUND_DEADLINE":
                return "Saturday 14:30";
            case "CORRECT_RESULTS":
                return "5";
            case "EXACT_SCORES":
                return "2";
            case "POINTS":
                return "18";
            case "TOP_SCORER":
                return "Sarah J";
            case "TOP_SCORER_POINTS":
                return "24";
            case "RESET_LINK":
                return $"{baseUrl}/authentication/reset-password?token=TEST-TOKEN";
            case "CONFIRM_LINK":
                return $"{baseUrl}/authentication/confirm-email?token=TEST-TOKEN";
            case "LOGIN_LINK":
                return $"{baseUrl}/authentication/login";
            case "DASHBOARD_URL":
                return $"{baseUrl}/dashboard";
            case "PREDICTIONS_URL":
                return $"{baseUrl}/predictions";
            case "LEAGUE_URL":
                return $"{baseUrl}/leagues";
            default:
                if (name.EndsWith("_URL", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_LINK", StringComparison.OrdinalIgnoreCase))
                    return baseUrl;

                // No specific rule: fall back to a readable placeholder so no input is left blank.
                return Humanise(name);
        }
    }

    private static string Humanise(string name)
    {
        var words = name
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

        return string.Join(' ', words);
    }
}
