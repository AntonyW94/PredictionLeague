using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's exact-scores leaderboard - who has predicted the most scorelines exactly.
///
/// Four rules left the SQL: the tie policy, the total, the display name, and the order of joint positions. There
/// is no rank-change arrow on this table, so unlike the overall and monthly leaderboards there is no
/// pre-round-position rule to go with them.
/// </summary>
public class GetExactScoresLeaderboardQueryHandler(
    IExactScoresLeaderboardQuery leaderboardQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetExactScoresLeaderboardQuery, ExactScoresLeaderboardDto>
{
    public async Task<ExactScoresLeaderboardDto> Handle(
        GetExactScoresLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await leaderboardQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        var totalsByUser = data.ExactScores
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.ExactScoreCount));

        // A member who has never predicted a scoreline exactly counts zero and appears at the bottom, rather
        // than being left off - which is what the ISNULL in the old SQL meant.
        var ranked = Ranking.ByDescending(
            data.Members,
            member => TotalFor(totalsByUser, member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return new ExactScoresLeaderboardDto
        {
            Entries = ranked
                .Select(entry => new ExactScoresLeaderboardEntryDto
                {
                    Rank = entry.Rank,
                    PlayerName = PlayerDisplayName.Format(entry.Item.FirstName, entry.Item.LastName),
                    ExactScoresCount = TotalFor(totalsByUser, entry.Item.UserId),
                    UserId = entry.Item.UserId
                })
                .ToList()
        };
    }

    private static int TotalFor(IReadOnlyDictionary<string, int> totalsByUser, string userId) =>
        totalsByUser.TryGetValue(userId, out var total) ? total : 0;
}
