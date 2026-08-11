using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's overall leaderboard.
///
/// The SQL that used to be here carried five rules: the tie policy (<c>RANK() OVER</c>), the total
/// (<c>COALESCE(SUM(...), 0)</c>), the display name, the order of joint positions, and when a pre-round
/// position is worth showing. All five are now C# and none of them depends on which database answered.
///
/// The pre-round position itself is still read from the cache maintained on the write path under ADR-0015 -
/// only the decision to show it moved.
/// </summary>
public class GetOverallLeaderboardQueryHandler(
    IOverallLeaderboardQuery leaderboardQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetOverallLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
        GetOverallLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await leaderboardQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        var totalsByUser = data.RoundPoints
            .GroupBy(points => points.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(points => points.BoostedPoints));

        // A member with no results scores zero and takes a position, rather than being left off the table -
        // which is what the COALESCE in the old SQL meant.
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
                // A pre-round position only means something once a round has been completed; before that there
                // is nothing to have moved from, so the arrow is hidden rather than shown against zero.
                SnapshotRank = LeaderboardSnapshot.RankToShow(entry.Item.SnapshotRank, data.HasCompletedRound),
                IsRoundInProgress = data.HasRoundInProgress
            })
            .ToList();
    }

    private static int TotalFor(IReadOnlyDictionary<string, int> totalsByUser, string userId) =>
        totalsByUser.TryGetValue(userId, out var total) ? total : 0;
}
