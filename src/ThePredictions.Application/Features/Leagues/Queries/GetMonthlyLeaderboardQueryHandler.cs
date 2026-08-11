using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One calendar month's leaderboard for a league.
///
/// The same five rules as the overall table left the SQL - tie policy, total, display name, order of joint
/// positions, and when the pre-round position shows - but the last of those is a different rule here, and
/// deliberately kept separate rather than shared with the overall table. See
/// <see cref="ShouldShowPreRoundPosition"/>.
/// </summary>
public class GetMonthlyLeaderboardQueryHandler(
    IMonthlyLeaderboardQuery leaderboardQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetMonthlyLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
        GetMonthlyLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await leaderboardQuery.ExecuteAsync(request.LeagueId, request.Month, cancellationToken);

        var totalsByUser = data.RoundPoints
            .GroupBy(points => points.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(points => points.BoostedPoints));

        var showPreRoundPosition = ShouldShowPreRoundPosition(data.MonthRoundStatuses);

        var ranked = Ranking.ByDescending(
            data.Members,
            member => TotalFor(totalsByUser, member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return ranked
            .Select(entry => new LeaderboardEntryDto
            {
                Rank = entry.Rank,
                PlayerName = PlayerDisplayName.Format(entry.Item.FirstName, entry.Item.LastName),
                TotalPoints = TotalFor(totalsByUser, entry.Item.UserId),
                UserId = entry.Item.UserId,
                SnapshotRank = showPreRoundPosition ? entry.Item.SnapshotRank : null,
                IsRoundInProgress = data.HasRoundInProgress
            })
            .ToList();
    }

    /// <summary>
    /// Whether a month's pre-round position is worth showing: a round in the month must be under way, and more
    /// than one of the month's rounds must have started.
    /// </summary>
    /// <remarks>
    /// Both conditions matter. Without the first there is no live round for the arrow to describe. Without the
    /// second the arrow would appear during a month's opening round, where the "position before this round" is
    /// the position before the month began - which is no position at all.
    ///
    /// Deliberately <b>not</b> shared with the overall table, whose rule is the simpler "any round in the season
    /// has been completed". They look similar and are not the same question, and collapsing them would silently
    /// start showing an arrow on a month's first round.
    /// </remarks>
    private static bool ShouldShowPreRoundPosition(IReadOnlyList<RoundStatus> monthRoundStatuses)
    {
        var anyUnderWay = monthRoundStatuses.Any(status => status == RoundStatus.InProgress);

        var startedCount = monthRoundStatuses
            .Count(status => status is RoundStatus.InProgress or RoundStatus.Completed);

        return anyUnderWay && startedCount > 1;
    }

    private static int TotalFor(IReadOnlyDictionary<string, int> totalsByUser, string userId) =>
        totalsByUser.TryGetValue(userId, out var total) ? total : 0;
}
