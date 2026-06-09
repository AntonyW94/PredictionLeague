namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Presentation helpers for the round-results digest email: ordinal positions and
/// human-readable position movement, since Brevo's template language can't compute them.
/// </summary>
public static class DigestEmailFormatter
{
    public static string Ordinal(int? position)
    {
        if (position is null || position < 1)
            return string.Empty;

        var n = position.Value;
        var suffix = (n % 100) is >= 11 and <= 13
            ? "th"
            : (n % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };

        return $"{n}{suffix}";
    }

    // Position-movement chip, mirroring the site's RankChangeArrow: green up triangle, red down
    // triangle, grey dash for no change. Positive delta = places gained this round. The three parts
    // are passed to the template separately so Brevo only needs a truthiness check on the arrow.

    private const string UpArrow = "▲";   // BLACK UP-POINTING TRIANGLE
    private const string DownArrow = "▼"; // BLACK DOWN-POINTING TRIANGLE
    private const string NoChangeDash = "-";

    private const string UpColour = "#00B960";       // --green-600
    private const string DownColour = "#E90052";     // --red
    private const string NoChangeColour = "#98a2b3"; // neutral grey

    /// <summary>Arrow glyph for the movement: up/down triangle, a dash for no change, empty when unavailable.</summary>
    public static string MovementArrow(int? delta) => delta switch
    {
        null => string.Empty,
        > 0 => UpArrow,
        < 0 => DownArrow,
        _ => NoChangeDash
    };

    /// <summary>Hex colour for the movement chip, empty when unavailable.</summary>
    public static string MovementColour(int? delta) => delta switch
    {
        null => string.Empty,
        > 0 => UpColour,
        < 0 => DownColour,
        _ => NoChangeColour
    };

    /// <summary>Magnitude of the movement as text (e.g. "3"); empty for no change or when unavailable.</summary>
    public static string MovementCount(int? delta) => delta is null or 0
        ? string.Empty
        : Math.Abs(delta.Value).ToString();
}
