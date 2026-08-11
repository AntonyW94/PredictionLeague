using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One player's season in one league: where they finished, their best and worst rounds, what they won, and the
/// highest position they ever held.
///
/// Four <c>RANK() OVER</c> windows in two statements. Two of them - rounds won and months won - are the same rule
/// the records tile asks from the other side, and are now <c>Wins.ByPeriod</c> for both. The fourth is the
/// interesting one: a running total recomputed round by round across the whole league, so the player can be told
/// they were second at some point in November even though they finished ninth.
/// </summary>
public class GetSeasonRecapQueryHandler(
    ISeasonRecapQuery recapQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetSeasonRecapQuery, SeasonRecapDto>
{
    public async Task<SeasonRecapDto> Handle(GetSeasonRecapQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.UserId, cancellationToken);

        var data = await recapQuery.ExecuteAsync(request.LeagueId, request.UserId, cancellationToken);

        if (data is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        // Only approved members count, as everywhere else on the site.
        var approvedUserIds = data.ApprovedMembers.Select(member => member.UserId).ToHashSet();

        var roundsById = data.SeasonRounds.ToDictionary(round => round.RoundId);

        var scored = data.RoundScores
            .Where(score => approvedUserIds.Contains(score.UserId) && roundsById.ContainsKey(score.RoundId))
            .Select(score => new ScoredRound(score.UserId, roundsById[score.RoundId], score.BoostedPoints))
            .ToList();

        var myScores = scored.Where(score => score.UserId == request.UserId).ToList();

        var completed = scored.Where(score => score.Round.Status == RoundStatus.Completed).ToList();

        var bestRound = LeagueRecords.Highest(myScores, score => score.Points, score => score.Round.RoundNumber, NoName);
        var worstRound = LeagueRecords.Lowest(myScores, score => score.Points, score => score.Round.RoundNumber, NoName);

        var totalWinnings = data.WinningAmounts.Sum();

        var trajectory = Trajectory(data, scored, request.UserId);

        return new SeasonRecapDto
        {
            IsFree = data.IsFree,
            LeaguePrice = data.LeaguePrice,

            FinalPosition = FinalPosition(data, scored, request.UserId),
            TotalMembers = data.ApprovedMembers.Count,
            TotalWinnings = totalWinnings,
            ProfitLoss = totalWinnings - data.LeaguePrice,

            AveragePointsPerRound = AveragePoints(myScores),
            BestRoundPoints = bestRound?.Points ?? 0,
            BestRoundNumber = bestRound?.Round.RoundNumber,
            WorstRoundPoints = worstRound?.Points ?? 0,
            WorstRoundNumber = worstRound?.Round.RoundNumber,
            TotalExactScores = data.ExactScoreCounts.Sum(),

            RoundsWon = CountWins(completed, score => score.Round.RoundId, request.UserId),
            MonthsWon = CountWins(completed, score => MonthOf(score.Round), request.UserId),

            HighestPosition = trajectory.HighestPosition,
            RoundsAtHighestPosition = trajectory.RoundsAtHighestPosition
        };
    }

    /// <summary>
    /// Where the player finished on total points, joint positions sharing a number as they do everywhere else.
    /// Zero when the player holds no position at all, which is what the old <c>ISNULL(..., 0)</c> said.
    /// </summary>
    private static int FinalPosition(SeasonRecapData data, IReadOnlyList<ScoredRound> scored, string userId)
    {
        var totals = TotalsByUserId(scored);

        var ranked = Ranking.ByDescending(
            data.ApprovedMembers,
            member => totals.GetValueOrDefault(member.UserId),
            member => PlayerDisplayName.FormatFull(member.FirstName, member.LastName));

        return ranked.FirstOrDefault(entry => entry.Item.UserId == userId)?.Rank ?? 0;
    }

    /// <summary>
    /// The player's average round score, over the rounds they have a result for rather than over the season.
    /// </summary>
    /// <remarks>
    /// No status filter, matching the old block: a round in progress that has already been scored counts. Nor is
    /// there a zero for a round they have no result in - the average is of rounds played, so missing a round does
    /// not drag it down.
    /// </remarks>
    private static decimal AveragePoints(IReadOnlyList<ScoredRound> myScores)
    {
        if (myScores.Count == 0)
            return 0;

        return myScores.Average(score => (decimal)score.Points);
    }

    private static int CountWins<TPeriod>(
        IReadOnlyList<ScoredRound> completed,
        Func<ScoredRound, TPeriod> periodSelector,
        string userId)
        where TPeriod : notnull =>
        Wins.ByPeriod(completed, periodSelector, score => score.UserId, score => score.Points)
            .Count(winnerId => winnerId == userId);

    /// <summary>
    /// The highest position the player ever held, and how many rounds they held it for.
    /// </summary>
    /// <remarks>
    /// Walks the season's completed rounds in order, adding each round's points to every member's running total and
    /// ranking the league as it stood. Rounds where nobody scored still count as a step, which is why the port
    /// returns the rounds themselves rather than only the rows with points in them.
    ///
    /// The <c>Total &gt; 0</c> guard is what stops the answer being "first, after round one" for a player who had
    /// not scored yet - before anyone scores, everyone is joint first.
    /// </remarks>
    private static (int HighestPosition, int RoundsAtHighestPosition) Trajectory(
        SeasonRecapData data,
        IReadOnlyList<ScoredRound> scored,
        string userId)
    {
        var scoresByRound = scored.ToLookup(score => score.Round.RoundId);

        var runningTotals = data.ApprovedMembers.ToDictionary(member => member.UserId, _ => 0);

        var positions = new List<(int Rank, int Total)>();

        foreach (var round in data.SeasonRounds
                     .Where(round => round.Status == RoundStatus.Completed)
                     .OrderBy(round => round.RoundNumber))
        {
            foreach (var score in scoresByRound[round.RoundId])
                runningTotals[score.UserId] += score.Points;

            var myTotal = runningTotals.GetValueOrDefault(userId);

            // The number of members strictly ahead, plus one - which is what RANK() gave, ties included.
            var rank = 1 + runningTotals.Values.Count(total => total > myTotal);

            positions.Add((rank, myTotal));
        }

        var qualifying = positions.Where(position => position.Total > 0).ToList();

        if (qualifying.Count == 0)
            return (0, 0);

        var highest = qualifying.Min(position => position.Rank);

        // Counted over every round, including any before the player had scored - so a completed round in which the
        // whole league was on nothing counts towards a position the player reached properly elsewhere. That is the
        // owner's decision (2026-08-11), not an inherited accident: a completed round happened, and whoever was top
        // of the league during it was top of the league.
        //
        // Note the guard above still applies to finding the position, and deliberately so. Without it a player who
        // was only ever joint first during a scoreless round would be told their highest position was first, which
        // would be true of every member of the league.
        var roundsAtHighest = positions.Count(position => position.Rank == highest);

        return (highest, roundsAtHighest);
    }

    private static Dictionary<string, int> TotalsByUserId(IReadOnlyList<ScoredRound> scored) =>
        scored
            .GroupBy(score => score.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(score => score.Points));

    private static (int Year, int Month) MonthOf(SeasonRecapRoundRow round) =>
        (round.StartDateUtc.Year, round.StartDateUtc.Month);

    /// <summary>
    /// The recap's records are one player's own, so there is never a second holder to order against - unlike the
    /// league records tile, where the name is the final tie-break.
    /// </summary>
    private static string NoName(ScoredRound score) => string.Empty;

    private sealed record ScoredRound(string UserId, SeasonRecapRoundRow Round, int Points);
}
