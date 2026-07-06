namespace ThePredictions.Contracts.Rounds;

/// <summary>
/// A single player's prediction-completion status for a round: how many of the currently
/// predictable fixtures they have entered, which ones they are still missing, and when (if ever)
/// they were last sent an ad-hoc reminder.
/// </summary>
public record RoundCompletionPlayerDto(
    string UserId,
    string PlayerName,
    string Email,
    int PredictedCount,
    DateTime? LastRemindedUtc,
    IReadOnlyList<MissingFixtureDto> MissingFixtures)
{
    /// <summary>Number of predictable fixtures the player has not yet entered.</summary>
    public int MissingCount => MissingFixtures.Count;

    /// <summary>True when the player has entered some, but not all, predictable fixtures.</summary>
    public bool IsPartial => PredictedCount > 0 && MissingCount > 0;

    /// <summary>True when the player has entered none of the predictable fixtures.</summary>
    public bool HasEnteredNothing => PredictedCount == 0 && MissingCount > 0;
}
