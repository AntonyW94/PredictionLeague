namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// What <c>TestDatabase.SeedLeagueAsync</c> created, so a journey can sign in as the right player and
/// navigate to the right league without looking anything up.
/// </summary>
/// <param name="PlayerEmail">
/// Unique to the seeding test class, which is what keeps classes from treading on each other - see the
/// remarks on <c>SeedLeagueAsync</c>.
/// </param>
internal sealed record SeededLeague(
    int SeasonId,
    int LeagueId,
    string LeagueName,
    string PlayerEmail,
    string PlayerPassword,
    int RoundId);
