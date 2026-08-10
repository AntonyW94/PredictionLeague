namespace ThePredictions.Persistence.Conformance;

/// <summary>
/// Everything a conformance test needs to read back, expressed without a dialect.
///
/// Assertions read directly rather than through the code under test, for the same reason arrangement
/// writes directly: asserting through the thing you are testing lets that thing agree with itself. Each
/// adapter implements this with its own statements.
///
/// The return types are plain records defined here, not adapter row types, so a test asserting on a
/// stored match compiles once and runs against every adapter.
/// </summary>
public interface ITestDataInspector
{
    /// <summary>Ids of every match currently attached to the round, in no guaranteed order.</summary>
    Task<IReadOnlyList<int>> MatchIdsForRoundAsync(int roundId);

    Task<int> PredictionCountForMatchAsync(int matchId);

    Task<bool> MatchExistsAsync(int matchId);

    /// <summary>The round a match currently belongs to, or null if the match is gone.</summary>
    Task<int?> RoundIdForMatchAsync(int matchId);

    Task<StoredMatch?> MatchAsync(int matchId);

    Task<StoredRound?> RoundAsync(int roundId);
}
