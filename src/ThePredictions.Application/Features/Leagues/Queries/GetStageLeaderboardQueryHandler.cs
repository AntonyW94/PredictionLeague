using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A tournament stage's leaderboard - the group stage or the knockout stage of a league's season.
///
/// The richest of the leaderboards, and the one that most repays moving. Seven rules were in that single
/// statement, including two ranks and a classification whose behaviour depended on the database collation.
///
/// The pre-round position here is <b>computed</b>, unlike the overall and monthly tables which read a cached
/// column. It is the position a member held over the stage's rounds excluding the one in progress - which is why
/// the old SQL had a second <c>RANK() OVER</c> nested inside a <c>CASE</c>.
/// </summary>
public class GetStageLeaderboardQueryHandler(
    IStageLeaderboardQuery leaderboardQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetStageLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(
        GetStageLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var data = await leaderboardQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        var stageRounds = data.SeasonRounds
            .Where(round => TournamentStageClassifier.ClassifyFrom(round.Stages) == request.Stage)
            .ToList();

        var stageRoundIds = stageRounds.Select(round => round.RoundId).ToHashSet();

        // The rounds already finished within this stage. Points from the round in progress are excluded from the
        // pre-round position, because that position is what a member held before the current round began.
        var settledRoundIds = stageRounds
            .Where(round => round.Status != RoundStatus.InProgress)
            .Select(round => round.RoundId)
            .ToHashSet();

        var totals = TotalsFor(data.RoundPoints, stageRoundIds);
        var preRoundTotals = TotalsFor(data.RoundPoints, settledRoundIds);

        var showPreRoundPosition = ShouldShowPreRoundPosition(stageRounds);

        var preRoundPositions = Ranking.ByDescending(
                data.Members,
                member => TotalFor(preRoundTotals, member.UserId),
                member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName))
            .ToDictionary(entry => entry.Item.UserId, entry => entry.Rank);

        var ranked = Ranking.ByDescending(
            data.Members,
            member => TotalFor(totals, member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return ranked
            .Select(entry => new LeaderboardEntryDto
            {
                Rank = entry.Rank,
                PlayerName = PlayerDisplayName.Format(entry.Item.FirstName, entry.Item.LastName),
                TotalPoints = TotalFor(totals, entry.Item.UserId),
                UserId = entry.Item.UserId,
                SnapshotRank = showPreRoundPosition ? preRoundPositions[entry.Item.UserId] : null,
                IsRoundInProgress = data.HasRoundInProgress
            })
            .ToList();
    }

    /// <summary>
    /// Whether the stage's pre-round position is worth showing: a round in the stage must be under way, and more
    /// than one of the stage's rounds must have started.
    /// </summary>
    /// <remarks>
    /// The same shape as the monthly leaderboard's rule but scoped to the stage's rounds rather than the month's,
    /// and kept separate for the same reason the monthly one is kept separate from the overall table's: the three
    /// look alike written as SQL and answer different questions. Without the second condition the arrow would
    /// appear during a stage's opening round, where the position before it is no position at all.
    /// </remarks>
    private static bool ShouldShowPreRoundPosition(IReadOnlyList<SeasonRoundStageRow> stageRounds)
    {
        var anyUnderWay = stageRounds.Any(round => round.Status == RoundStatus.InProgress);

        var startedCount = stageRounds
            .Count(round => round.Status is RoundStatus.InProgress or RoundStatus.Completed);

        return anyUnderWay && startedCount > 1;
    }

    private static Dictionary<string, int> TotalsFor(
        IReadOnlyList<MemberRoundPointsByRoundRow> points,
        IReadOnlySet<int> roundIds) =>
        points
            .Where(row => roundIds.Contains(row.RoundId))
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.BoostedPoints));

    private static int TotalFor(IReadOnlyDictionary<string, int> totals, string userId) =>
        totals.TryGetValue(userId, out var total) ? total : 0;
}
