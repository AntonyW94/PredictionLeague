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
/// </summary>
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

        var topRound = HighestRound(data.RoundScores);
        var lowestRound = LowestRound(data.RoundScores);
        var mostExact = MostExactInARound(data.ExactScores);
        var champion = Champion(data.ApprovedMembers, data.RoundScores);
        var topEarner = TopEarner(data.Winnings);
        var mostRoundsWon = MostRoundsWon(data.RoundScores);
        var mostMonthsWon = MostMonthsWon(data.RoundScores);
        var biggestPrize = BiggestPrize(data.Winnings);
        var topGameweek = HighestScoringRound(data.RoundScores);

        return new LeagueRecordsDto
        {
            IsFree = data.IsFree,

            TopRoundPlayerName = NameOf(topRound?.FirstName, topRound?.LastName),
            TopRoundPoints = topRound?.BoostedPoints ?? 0,
            TopRoundNumber = topRound?.RoundNumber,

            LowestRoundPlayerName = NameOf(lowestRound?.FirstName, lowestRound?.LastName),
            LowestRoundPoints = lowestRound?.BoostedPoints ?? 0,
            LowestRoundNumber = lowestRound?.RoundNumber,

            MostExactInRoundPlayerName = NameOf(mostExact?.FirstName, mostExact?.LastName),
            MostExactInRoundCount = mostExact?.ExactScoreCount ?? 0,
            MostExactInRoundNumber = mostExact?.RoundNumber,

            ChampionName = NameOf(champion?.FirstName, champion?.LastName),
            ChampionPoints = champion?.Points ?? 0,

            TopEarnerName = NameOf(topEarner?.FirstName, topEarner?.LastName),
            TopEarnerAmount = topEarner?.Amount ?? 0,

            MostRoundsWonPlayerName = NameOf(mostRoundsWon?.FirstName, mostRoundsWon?.LastName),
            MostRoundsWonCount = mostRoundsWon?.WinCount ?? 0,

            MostMonthsWonPlayerName = NameOf(mostMonthsWon?.FirstName, mostMonthsWon?.LastName),
            MostMonthsWonCount = mostMonthsWon?.WinCount ?? 0,

            TotalExactScores = data.ExactScores.Sum(row => row.ExactScoreCount),

            BiggestPrizePlayerName = NameOf(biggestPrize?.FirstName, biggestPrize?.LastName),
            BiggestPrizeAmount = biggestPrize?.Amount ?? 0,
            BiggestPrizeDescription = biggestPrize is null
                ? null
                : PrizeDescription.For(
                    biggestPrize.PrizeDescription,
                    biggestPrize.PrizeType,
                    biggestPrize.RoundNumber,
                    biggestPrize.Month),

            HighestGameweekRoundNumber = topGameweek?.RoundNumber,
            HighestGameweekPoints = topGameweek?.Points ?? 0
        };
    }

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
    /// Who has won the most rounds. A round is won by whoever scored most in it, joint winners each counting a
    /// win, and a round nobody scored in is won by nobody.
    /// </summary>
    /// <remarks>
    /// The old block ranked with <c>RANK() OVER (PARTITION BY [RoundId] ...)</c> and kept
    /// <c>Rnk = 1 AND BoostedPoints > 0</c>. Both halves matter: <c>RANK</c> rather than <c>ROW_NUMBER</c> is what
    /// lets a shared win count for both players, and the points test stops a round that has been created but not
    /// yet scored handing everyone in the league a win.
    /// </remarks>
    private static PlayerCount? MostRoundsWon(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        MostWins(scores
            .Where(row => row.RoundStatus == RoundStatus.Completed)
            .GroupBy(row => row.RoundId)
            .SelectMany(round => WinnersOf(round.Select(Entry).ToList())));

    /// <summary>
    /// Who has won the most months. A month is won on the total of its completed rounds, and a round belongs to
    /// the calendar month it started in.
    /// </summary>
    private static PlayerCount? MostMonthsWon(IReadOnlyList<LeagueRecordRoundScoreRow> scores) =>
        MostWins(scores
            .Where(row => row.RoundStatus == RoundStatus.Completed)
            .GroupBy(row => new { row.RoundStartDateUtc.Year, row.RoundStartDateUtc.Month })
            .SelectMany(month => WinnersOf(month
                .GroupBy(row => row.UserId)
                .Select(player => new ScoreEntry(
                    player.Key,
                    player.First().FirstName,
                    player.First().LastName,
                    player.Sum(row => row.BoostedPoints)))
                .ToList())));

    /// <summary>
    /// The winners of one round or one month: everyone tied on the best score, provided that score beat nothing.
    /// </summary>
    private static IReadOnlyList<ScoreEntry> WinnersOf(IReadOnlyList<ScoreEntry> contenders)
    {
        var best = contenders.Max(contender => contender.Points);

        if (best <= 0)
            return [];

        return contenders.Where(contender => contender.Points == best).ToList();
    }

    private static PlayerCount? MostWins(IEnumerable<ScoreEntry> wins)
    {
        var counts = wins
            .GroupBy(win => win.UserId)
            .Select(group => new PlayerCount(
                group.First().FirstName,
                group.First().LastName,
                group.Count()))
            .ToList();

        return LeagueRecords.Highest(counts, count => count.WinCount, _ => 0, FullName);
    }

    private static ScoreEntry Entry(LeagueRecordRoundScoreRow row) =>
        new(row.UserId, row.FirstName, row.LastName, row.BoostedPoints);

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

    private static string? NameOf(string? firstName, string? lastName) =>
        firstName is null || lastName is null ? null : PlayerDisplayName.Format(firstName, lastName);

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

    /// <summary>One contender's score in one round or one month, ready to be compared with its peers.</summary>
    private sealed record ScoreEntry(string UserId, string FirstName, string LastName, int Points);

    private sealed record PlayerTotal(string FirstName, string LastName, int Points);

    private sealed record PlayerAmount(string FirstName, string LastName, decimal Amount);

    private sealed record PlayerCount(string FirstName, string LastName, int WinCount);

    private sealed record RoundTotal(int RoundNumber, int Points);
}
