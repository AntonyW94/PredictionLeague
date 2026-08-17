namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// What <c>TestDatabase.SeedPrivateLeagueToJoinAsync</c> created: a private league the player holds a Season
/// Pass for and is <b>not</b> a member of, so a journey can join it by code.
/// </summary>
internal sealed record SeededPrivateLeague(
    int SeasonId,
    int LeagueId,
    string EntryCode,
    string PlayerEmail,
    string PlayerPassword);
