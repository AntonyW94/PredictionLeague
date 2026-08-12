using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A league's records tile - its highest round, its worst round, its champion, its top earner and six more.
///
/// The largest single statement in the application: ten <c>OUTER APPLY</c> blocks, each one a
/// <c>SELECT TOP 1 ... ORDER BY</c> that picked a winner, plus two <c>RANK() OVER</c> windows for the wins
/// counts. Every one of those choices was a rule, and four of them had no tie-break at all - so which of two joint
/// record-holders got named was the query plan's decision and could change between page loads.
///
/// Who may hold a record is now one rule rather than ten: an approved member of the league. The old blocks
/// disagreed - five read <c>LeagueRoundResults</c> with no membership check, two required <c>Approved</c> - which
/// meant a player outside the league could in principle hold the highest-round record while being ineligible for
/// the most-exact-scores one, and could not appear on any leaderboard that would corroborate it.
/// </summary>
/// <remarks>
/// The filter changes nothing today and is not meant to: <c>Approved</c> is a terminal state (nothing transitions
/// out of it) and the results write path only writes for approved members, so a score against a non-member is
/// unreachable. It is here so that the day a player can leave or be removed, the tile follows the same population
/// as every leaderboard on the site without anyone having to remember these ten records exist.
///
/// An adapter may still narrow at the source - the exact-score read does, because <c>RoundResults</c> is
/// league-agnostic and reading every player's season to discard most of it would be wasteful. Narrowing can only
/// remove rows this filter would remove anyway, so it is an optimisation rather than a second copy of the rule.
/// </remarks>
public class GetLeagueRecordsQueryHandler(
    ILeagueRecordsQuery recordsQuery,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueRecordsQuery, LeagueRecordsDto>
{
    public async Task<LeagueRecordsDto> Handle(GetLeagueRecordsQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.UserId, cancellationToken);

        var data = await recordsQuery.ExecuteAsync(request.LeagueId, cancellationToken);

        if (data is null)
            throw new EntityNotFoundException("League", request.LeagueId);

        // Every record belongs to an approved member of the league - stated once here rather than ten times.
        var approvedUserIds = data.ApprovedMembers.Select(member => member.UserId).ToHashSet();

        var roundScores = data.RoundScores.Where(row => approvedUserIds.Contains(row.UserId)).ToList();
        var exactScores = data.ExactScores.Where(row => approvedUserIds.Contains(row.UserId)).ToList();
        var winnings = data.Winnings.Where(row => approvedUserIds.Contains(row.UserId)).ToList();

        var topRound = InARound(HighestRound(roundScores));
        var lowestRound = InARound(LowestRound(roundScores));
        var mostExact = ExactScoresInARound(MostExactInARound(exactScores));
        var champion = OverTheSeason(Champion(data.ApprovedMembers, roundScores));
        var topEarner = Earned(TopEarner(winnings));
        var mostRoundsWon = Won(MostRoundsWon(roundScores));
        var mostMonthsWon = Won(MostMonthsWon(roundScores));
        var biggestPrize = SinglePrize(BiggestPrize(winnings));
        var topGameweek = WholeLeaguesRound(HighestScoringRound(roundScores));

        return new LeagueRecordsDto
        {
            IsFree = data.IsFree,

            TopRoundPlayerName = topRound.PlayerName,
            TopRoundPoints = topRound.Points,
            TopRoundNumber = topRound.RoundNumber,

            LowestRoundPlayerName = lowestRound.PlayerName,
            LowestRoundPoints = lowestRound.Points,
            LowestRoundNumber = lowestRound.RoundNumber,

            MostExactInRoundPlayerName = mostExact.PlayerName,
            MostExactInRoundCount = mostExact.Count,
            MostExactInRoundNumber = mostExact.RoundNumber,

            ChampionName = champion.PlayerName,
            ChampionPoints = champion.Points,

            TopEarnerName = topEarner.PlayerName,
            TopEarnerAmount = topEarner.Amount,

            MostRoundsWonPlayerName = mostRoundsWon.PlayerName,
            MostRoundsWonCount = mostRoundsWon.Count,

            MostMonthsWonPlayerName = mostMonthsWon.PlayerName,
            MostMonthsWonCount = mostMonthsWon.Count,

            TotalExactScores = exactScores.Sum(row => row.ExactScoreCount),

            BiggestPrizePlayerName = biggestPrize.PlayerName,
            BiggestPrizeAmount = biggestPrize.Amount,
            BiggestPrizeDescription = biggestPrize.Description,

            HighestGameweekRoundNumber = topGameweek.RoundNumber,
            HighestGameweekPoints = topGameweek.Points
        };
    }

    #region Turning "nobody holds this record" into the values the tile shows

    // A record with no holder is the ordinary case on a league that has not started, and the tile shows a dash and a
    // zero rather than hiding the row. Each of these resolvers answers that for one record, so the question is asked
    // once per record type instead of three times per record in the middle of building the reply - which is what made
    // Handle the most complex method in the solution while doing nothing but assignment.

    private static PlayerRecord InARound(LeagueRecordRoundScoreRow? row) =>
        row is null
            ? PlayerRecord.None
            : new PlayerRecord(NameOf(row.FirstName, row.LastName), row.BoostedPoints, row.RoundNumber);

    private static PlayerRecord ExactScoresInARound(LeagueRecordExactScoreRow? row) =>
        row is null
            ? PlayerRecord.None
            : new PlayerRecord(NameOf(row.FirstName, row.LastName), row.ExactScoreCount, row.RoundNumber);

    private static PlayerTally OverTheSeason(PlayerTotal? total) =>
        total is null ? PlayerTally.None : new PlayerTally(NameOf(total.FirstName, total.LastName), total.Points);

    private static PlayerTally Won(PlayerCount? count) =>
        count is null ? PlayerTally.None : new PlayerTally(NameOf(count.FirstName, count.LastName), count.WinCount);

    private static PlayerMoney Earned(PlayerAmount? total) =>
        total is null ? PlayerMoney.None : new PlayerMoney(NameOf(total.FirstName, total.LastName), total.Amount);

    /// <summary>
    /// The largest single prize, with the label describing what it was for - built here rather than read from a
    /// column, because the old statement finished it with <c>DATENAME(MONTH, ...)</c>.
    /// </summary>
    private static PrizeRecord SinglePrize(LeagueRecordWinningRow? row) =>
        row is null
            ? PrizeRecord.None
            : new PrizeRecord(
                NameOf(row.FirstName, row.LastName),
                row.Amount,
                PrizeDescription.For(row.PrizeDescription, row.PrizeType, row.RoundNumber, row.Month));

    /// <summary>The league's best round as a whole, which belongs to no one player.</summary>
    private static RoundRecord WholeLeaguesRound(RoundTotal? total) =>
        total is null ? RoundRecord.None : new RoundRecord(total.RoundNumber, total.Points);

    private static string NameOf(string firstName, string lastName) =>
        PlayerDisplayName.Format(firstName, lastName);

    #endregion

    /// <summary>The best single round anyone has had, earliest round first if two match.</summary>
    private static LeagueRecordRoundScoreRow? HighestRound(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        LeagueRecords.Highest(scores, row => row.BoostedPoints, row => row.RoundNumber, FullName);

    /// <summary>
    /// The worst single round anyone has had - but only counting rounds they actually entered.
    /// </summary>
    /// <remarks>
    /// The old block's <c>EXISTS</c> over <c>UserPredictions</c>. Without it the record would belong to whoever
    /// joined most recently, holding a nil score for rounds that closed before they arrived, which is a record
    /// about administration rather than about football.
    /// </remarks>
    private static LeagueRecordRoundScoreRow? LowestRound(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        LeagueRecords.Lowest(
            scores.Where(row => row.HasAnyPrediction).ToList(),
            row => row.BoostedPoints,
            row => row.RoundNumber,
            FullName);

    private static LeagueRecordExactScoreRow? MostExactInARound(IReadOnlyList<LeagueRecordExactScoreRow> scores) =>
        LeagueRecords.Highest(scores, row => row.ExactScoreCount, row => row.RoundNumber, FullName);

    /// <summary>
    /// The league's leader on total points. Every approved member is a candidate, including one who has never
    /// scored - which is what the old block's <c>LEFT JOIN</c> and <c>ISNULL(SUM(...), 0)</c> meant.
    /// </summary>
    private static PlayerTotal? Champion(
        IReadOnlyList<LeaderboardParticipantRow> members,
        IReadOnlyList<LeagueRecordRoundScoreRow> scores)
    {
        var pointsByUserId = scores
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.BoostedPoints));

        var totals = members
            .Select(member => new PlayerTotal(
                member.FirstName,
                member.LastName,
                pointsByUserId.TryGetValue(member.UserId, out var points) ? points : 0))
            .ToList();

        return LeagueRecords.Highest(totals, total => total.Points, _ => 0, FullName);
    }

    private static PlayerAmount? TopEarner(IReadOnlyList<LeagueRecordWinningRow> winnings)
    {
        var totals = winnings
            .GroupBy(row => row.UserId)
            .Select(group => new PlayerAmount(
                group.First().FirstName,
                group.First().LastName,
                group.Sum(row => row.Amount)))
            .ToList();

        return LeagueRecords.Highest(totals, total => total.Amount, _ => 0, FullName);
    }

    /// <summary>
    /// Who has won the most rounds. Which rounds were won is <c>Wins.ByPeriod</c>; this counts them per player and
    /// picks the leader.
    /// </summary>
    private static PlayerCount? MostRoundsWon(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        MostWins(
            scores,
            Wins.ByPeriod(
                Completed(scores),
                row => row.RoundId,
                row => row.UserId,
                row => row.BoostedPoints));

    /// <summary>
    /// Who has won the most months. A round belongs to the calendar month it started in, and a month is won on the
    /// total of its rounds rather than by winning its best one.
    /// </summary>
    private static PlayerCount? MostMonthsWon(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        MostWins(
            scores,
            Wins.ByPeriod(
                Completed(scores),
                row => (row.RoundStartDateUtc.Year, row.RoundStartDateUtc.Month),
                row => row.UserId,
                row => row.BoostedPoints));

    private static IReadOnlyList<LeagueRecordRoundScoreRow> Completed(
        IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        scores.Where(row => row.RoundStatus == RoundStatus.Completed).ToList();

    /// <summary>
    /// The player with the most wins, named from whichever of their score rows came first - any of them carries
    /// their name.
    /// </summary>
    private static PlayerCount? MostWins(
        IReadOnlyList<LeagueRecordRoundScoreRow> scores,
        IReadOnlyList<string> winnerIds)
    {
        var namesByUserId = scores
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.First());

        var counts = winnerIds
            .GroupBy(userId => userId)
            .Select(group => new PlayerCount(
                namesByUserId[group.Key].FirstName,
                namesByUserId[group.Key].LastName,
                group.Count()))
            .ToList();

        return LeagueRecords.Highest(counts, count => count.WinCount, _ => 0, FullName);
    }

    /// <summary>
    /// The largest single prize paid. Ties go to whichever was awarded first, which is the one piece of
    /// tie-breaking the old statement did outside of round numbers.
    /// </summary>
    private static LeagueRecordWinningRow? BiggestPrize(IReadOnlyList<LeagueRecordWinningRow> winnings) =>
        LeagueRecords.Highest(winnings, row => row.Amount, row => row.AwardedDateUtc, FullName);

    /// <summary>The round in which the league as a whole scored most, earliest first if two match.</summary>
    private static RoundTotal? HighestScoringRound(IReadOnlyList<LeagueRecordRoundScoreRow> scores)
    {
        var totals = scores
            .GroupBy(row => new { row.RoundId, row.RoundNumber })
            .Select(group => new RoundTotal(group.Key.RoundNumber, group.Sum(row => row.BoostedPoints)))
            .ToList();

        return LeagueRecords.Highest(totals, total => total.Points, total => total.RoundNumber, _ => string.Empty);
    }

    private static string FullName(LeagueRecordRoundScoreRow row) =>
        PlayerDisplayName.FormatFull(row.FirstName, row.LastName);

    private static string FullName(LeagueRecordExactScoreRow row) =>
        PlayerDisplayName.FormatFull(row.FirstName, row.LastName);

    private static string FullName(LeagueRecordWinningRow row) =>
        PlayerDisplayName.FormatFull(row.FirstName, row.LastName);

    private static string FullName(PlayerTotal total) =>
        PlayerDisplayName.FormatFull(total.FirstName, total.LastName);

    private static string FullName(PlayerAmount total) =>
        PlayerDisplayName.FormatFull(total.FirstName, total.LastName);

    private static string FullName(PlayerCount count) =>
        PlayerDisplayName.FormatFull(count.FirstName, count.LastName);

    private sealed record PlayerTotal(string FirstName, string LastName, int Points);

    private sealed record PlayerAmount(string FirstName, string LastName, decimal Amount);

    private sealed record PlayerCount(string FirstName, string LastName, int WinCount);

    private sealed record RoundTotal(int RoundNumber, int Points);

    // One shape per record, rather than one shape with a spare field for the records that have no round or no
    // wording. A field only some of them mean is a field nothing can tell you is wrong: the mutation check proved it
    // by giving the champion a round number, which changed no test because nothing reads it.

    /// <summary>A record set in one round, counted in points or in exact scores.</summary>
    private sealed record PlayerRecord(string? PlayerName, int Points, int? RoundNumber)
    {
        /// <summary>Nobody holds it yet: no name, nothing scored, no round.</summary>
        internal static readonly PlayerRecord None = new(null, 0, null);

        /// <summary>The same number, named for what it counts where that is not points.</summary>
        internal int Count => Points;
    }

    /// <summary>A record held across the whole season: total points, rounds won or months won.</summary>
    private sealed record PlayerTally(string? PlayerName, int Points)
    {
        internal static readonly PlayerTally None = new(null, 0);

        internal int Count => Points;
    }

    /// <summary>The most money one player has taken out of the league.</summary>
    private sealed record PlayerMoney(string? PlayerName, decimal Amount)
    {
        internal static readonly PlayerMoney None = new(null, 0m);
    }

    /// <summary>The largest single prize, and the wording for what it was won for.</summary>
    private sealed record PrizeRecord(string? PlayerName, decimal Amount, string? Description)
    {
        internal static readonly PrizeRecord None = new(null, 0m, null);
    }

    /// <summary>A record belonging to a round rather than to a player.</summary>
    private sealed record RoundRecord(int? RoundNumber, int Points)
    {
        internal static readonly RoundRecord None = new(null, 0);
    }
}
