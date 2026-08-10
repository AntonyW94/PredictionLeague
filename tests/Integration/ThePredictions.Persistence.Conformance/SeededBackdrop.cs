namespace ThePredictions.Persistence.Conformance;

/// <summary>
/// The identities of the rows <see cref="ITestDataSeeder.AddBackdropAsync"/> created, so a test can hang
/// rounds, matches and predictions off them without re-deriving ids.
/// </summary>
public sealed record SeededBackdrop(
    int CompetitionId,
    int SeasonId,
    int HomeTeamId,
    int AwayTeamId,
    string UserId);
