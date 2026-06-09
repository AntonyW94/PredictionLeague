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

    /// <summary>
    /// Positive delta = places gained this round. Returns "up N" / "down N" / "no change",
    /// or empty when no delta is available.
    /// </summary>
    public static string PositionMovement(int? delta)
    {
        if (delta is null)
            return string.Empty;

        if (delta.Value > 0)
            return $"up {delta.Value}";

        if (delta.Value < 0)
            return $"down {-delta.Value}";

        return "no change";
    }
}
