namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The order league tiles appear in on the dashboard: whichever has a round in play first, then by when its season
/// started, then by stake highest first, then by name.
/// </summary>
/// <remarks>
/// Stake descending puts the league a player has most at risk above the free one they joined for fun.
///
/// Both dashboard tiles show this rule, and between them they had three copies of it - the leaderboards tile stated
/// it as a SQL <c>ORDER BY</c> and again as an identical LINQ chain over the same rows, and the My Leagues tile had
/// its own <c>ORDER BY</c>. Ordering is a presentation rule, so it lives here rather than in the domain.
/// </remarks>
public static class LeagueTileOrder
{
    public static IEnumerable<T> Apply<T>(IEnumerable<T> tiles) where T : ILeagueTile =>
        tiles
            .OrderBy(tile => tile.HasRoundInProgress ? 0 : 1)
            .ThenBy(tile => tile.SeasonStartDateUtc)
            .ThenByDescending(tile => tile.Price)
            .ThenBy(tile => tile.LeagueName);
}
