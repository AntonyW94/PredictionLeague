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

    /// <summary>Sample content that does not depend on the user or the environment.</summary>
    private static readonly IReadOnlyDictionary<string, string> SampleValues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["LEAGUE_NAME"] = "Test League",
        ["SEASON_NAME"] = "Test Season 2026",
        ["ROUND_NAME"] = "Round 1",
        ["NEXT_ROUND_NAME"] = "Round 2",
        ["DEADLINE"] = "Saturday 14:30",
        ["NEXT_ROUND_DEADLINE"] = "Saturday 14:30",
        ["CORRECT_RESULTS"] = "5",
        ["EXACT_SCORES"] = "2",
        ["POINTS"] = "18",
        ["TOP_SCORER"] = "Sarah J",
        ["TOP_SCORER_POINTS"] = "24"
    };

    /// <summary>Paths appended to the environment's base URL to build a working link.</summary>
    private static readonly IReadOnlyDictionary<string, string> LinkPaths = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RESET_LINK"] = "/authentication/reset-password?token=TEST-TOKEN",
        ["CONFIRM_LINK"] = "/authentication/confirm-email?token=TEST-TOKEN",
        ["LOGIN_LINK"] = "/authentication/login",
        ["DASHBOARD_URL"] = "/dashboard",
        ["PREDICTIONS_URL"] = "/predictions",
        ["LEAGUE_URL"] = "/leagues"
    };

    private static string ResolveOne(string name, EmailTestUserData user, string baseUrl)
    {
        var key = name.ToUpperInvariant();

        var fromUser = ResolveUserField(key, user);
        if (fromUser is not null)
            return fromUser;

        if (LinkPaths.TryGetValue(key, out var path))
            return baseUrl + path;

        if (SampleValues.TryGetValue(key, out var value))
            return value;

        if (name.EndsWith("_URL", StringComparison.OrdinalIgnoreCase) || name.EndsWith("_LINK", StringComparison.OrdinalIgnoreCase))
            return baseUrl;

        // No specific rule: fall back to a readable placeholder so no input is left blank.
        return Humanise(name);
    }

    /// <summary>Returns null when the name is not one of the user-derived fields.</summary>
    private static string? ResolveUserField(string key, EmailTestUserData user) => key switch
    {
        "FIRST_NAME" or "ADMIN_NAME" => user.FirstName,
        "LAST_NAME" => user.LastName,
        "NAME" or "FULL_NAME" => $"{user.FirstName} {user.LastName}".Trim(),
        "EMAIL" => user.Email,
        _ => null
    };

    private static string Humanise(string name)
    {
        // RemoveEmptyEntries guarantees every word has at least one character, so there is no
        // empty-string case to guard against here.
        var words = name
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

        return string.Join(' ', words);
    }
}
