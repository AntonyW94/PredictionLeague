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
            case "DEADLINE":
                return "Saturday 14:30";
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

                return string.Empty;
        }
    }
}
