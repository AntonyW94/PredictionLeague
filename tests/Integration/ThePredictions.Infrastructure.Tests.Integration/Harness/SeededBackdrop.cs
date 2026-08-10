namespace ThePredictions.Infrastructure.Tests.Integration.Harness;

/// <summary>
/// The identities of the rows <see cref="TestDataSeeder.AddBackdropAsync"/> created, so a test can hang
/// rounds, matches and predictions off them without re-deriving ids.
/// </summary>
internal sealed record SeededBackdrop(
    int CompetitionId,
    int SeasonId,
    int HomeTeamId,
    int AwayTeamId,
    string UserId);
