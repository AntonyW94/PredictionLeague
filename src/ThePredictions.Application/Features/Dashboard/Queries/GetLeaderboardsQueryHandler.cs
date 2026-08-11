using MediatR;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The dashboard's leaderboards tile: a small table for each league the player belongs to.
///
/// Seven rules were in one windowed CTE, and one of them was already in two places at once - the tile's league
/// ordering was written as a SQL <c>ORDER BY</c> and then written again as a LINQ <c>OrderBy</c> chain over the
/// same result, in this file. Only the C# copy decided anything.
/// </summary>
public class GetLeaderboardsQueryHandler(IDashboardLeaderboardsQuery leaderboardsQuery)
    : IRequestHandler<GetLeaderboardsQuery, IEnumerable<LeagueLeaderboardDto>>
{
    public async Task<IEnumerable<LeagueLeaderboardDto>> Handle(
        GetLeaderboardsQuery request,
        CancellationToken cancellationToken)
    {
        var data = await leaderboardsQuery.ExecuteAsync(request.UserId, cancellationToken);

        var membersByLeague = data.Members
            .GroupBy(member => member.LeagueId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var totalsByLeague = data.Points
            .GroupBy(row => row.LeagueId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => row.UserId)
                    .ToDictionary(byUser => byUser.Key, byUser => byUser.Sum(row => row.BoostedPoints)));

        return OrderLeagues(data.Leagues)
            .Select(league => ToDto(league, membersByLeague, totalsByLeague))
            .ToList();
    }

    /// <summary>
    /// The order the tiles appear in: a league with a round under way first, then by when its season started,
    /// then by stake highest first, then by name.
    /// </summary>
    /// <remarks>
    /// Stake descending puts the league a player has most at risk above the free one they joined for fun. This
    /// was the rule stated twice in the same file - as a four-clause <c>ORDER BY</c> whose result was then
    /// re-sorted by an identical LINQ chain, so the SQL half was doing nothing but making the two look like they
    /// had to agree.
    /// </remarks>
    private static IEnumerable<DashboardLeagueRow> OrderLeagues(IReadOnlyList<DashboardLeagueRow> leagues) =>
        leagues
            .OrderBy(league => league.HasRoundInProgress ? 0 : 1)
            .ThenBy(league => league.SeasonStartDateUtc)
            .ThenByDescending(league => league.Price)
            .ThenBy(league => league.LeagueName);

    private static LeagueLeaderboardDto ToDto(
        DashboardLeagueRow league,
        IReadOnlyDictionary<int, List<DashboardLeagueMemberRow>> membersByLeague,
        IReadOnlyDictionary<int, Dictionary<string, int>> totalsByLeague)
    {
        var members = membersByLeague.TryGetValue(league.LeagueId, out var found) ? found : [];
        var totals = totalsByLeague.TryGetValue(league.LeagueId, out var scored) ? scored : [];

        var hasCompletedRound = league.CompletedRoundCount > 0;

        var ranked = Ranking.ByDescending(
            members,
            member => TotalFor(totals, member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return new LeagueLeaderboardDto
        {
            LeagueId = league.LeagueId,
            LeagueName = league.LeagueName,
            SeasonName = league.SeasonName,
            IsFinished = SeasonCompletion.IsFinished(league.CompletedRoundCount, league.NumberOfRounds),
            IsArchivedByUser = league.IsArchivedByUser,
            Entries = ranked
                .Select(entry => new LeaderboardEntryDto
                {
                    Rank = entry.Rank,
                    PlayerName = PlayerDisplayName.Format(entry.Item.FirstName, entry.Item.LastName),
                    TotalPoints = TotalFor(totals, entry.Item.UserId),
                    UserId = entry.Item.UserId,
                    SnapshotRank = LeaderboardSnapshot.RankToShow(entry.Item.SnapshotRank, hasCompletedRound),
                    IsRoundInProgress = league.HasRoundInProgress
                })
                .ToList()
        };
    }

    /// <summary>
    /// A member with no result rows in a league scores zero rather than dropping out of its table, which is what
    /// the old <c>SUM(ISNULL(lrr.[BoostedPoints], 0))</c> over a left join was for.
    /// </summary>
    private static int TotalFor(IReadOnlyDictionary<string, int> totals, string userId) =>
        totals.TryGetValue(userId, out var total) ? total : 0;
}
