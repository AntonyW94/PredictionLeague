namespace ThePredictions.Domain.Services;

/// <summary>
/// How a player's name is shown to other players: first name plus last initial, "Ada L".
///
/// This is a presentation rule about our own domain, and it was written out in SQL in seventeen separate
/// files as <c>FirstName + ' ' + LEFT(LastName, 1)</c> - the most duplicated rule in the codebase. Seventeen
/// copies cannot be changed together, and the symptom of a divergence would be the same player appearing
/// under two different names on two different screens.
/// </summary>
public static class PlayerDisplayName
{
    /// <summary>
    /// First name plus the initial of the last name. Both parts are trimmed, so a missing last name yields
    /// the first name alone rather than a trailing space - which is what the SQL produced. The schema
    /// forbids either being null, so that case is defensive rather than expected.
    /// </summary>
    public static string Format(string? firstName, string? lastName)
    {
        var first = (firstName ?? string.Empty).Trim();
        var last = (lastName ?? string.Empty).Trim();

        return last.Length == 0 ? first : $"{first} {last[..1]}".Trim();
    }
}
