using MediatR;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The My Leagues tile: one card per league the player belongs to, showing where they stand, which round is on, and
/// what the pot is worth.
///
/// The largest read on the site, and the one with a performance history - it grew until SQL Server spent longer
/// planning it than running it (ADR-0015). Every rank here is still read from the <c>LeagueMemberStats</c> cache and
/// none is recomputed; what moved is the dozen rules that were wrapped around them.
/// </summary>
public class GetMyLeaguesQueryHandler(
    IMyLeaguesQuery myLeaguesQuery,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        var data = await myLeaguesQuery.ExecuteAsync(request.UserId, cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        var roundsBySeason = data.SeasonRounds.ToLookup(round => round.SeasonId);
        var scoresByLeague = data.RoundScores.ToLookup(score => score.LeagueId);
        var statsByLeague = data.Stats.ToDictionary(stats => stats.LeagueId);

        var tiles = data.Leagues
            .Select(league =>
            {
                var seasonRounds = roundsBySeason[league.SeasonId].ToList();

                var activeRound = ActiveRound.Of(
                    seasonRounds,
                    utcNow,
                    round => round.Status,
                    round => round.CompletedDateUtc,
                    round => round.RoundNumber);

                return new LeagueTile(
                    league,
                    activeRound,
                    statsByLeague.GetValueOrDefault(league.LeagueId),
                    seasonRounds);
            })
            .ToList();

        return LeagueTileOrder.Apply(tiles)
            .Select(tile => ToDto(tile, scoresByLeague[tile.League.LeagueId], request.UserId))
            .ToList();
    }

    private static MyLeagueDto ToDto(
        LeagueTile tile,
        IEnumerable<MyLeagueRoundScoreRow> leagueScores,
        string userId)
    {
        var league = tile.League;
        var round = tile.ActiveRound;
        var stats = tile.Stats;

        var isTournament = league.CompetitionType == CompetitionType.Tournament;

        var totalPrizeFund = PrizeFund.Total(league.Price, league.MemberCount, league.PrizeFundOverride);

        var completedScores = CompletedScores(tile, leagueScores);

        return new MyLeagueDto(
            league.LeagueId,
            league.LeagueName,
            league.SeasonName,
            league.CompetitionType,
            league.SeasonStartDateUtc,
            league.EntryDeadlineUtc,
            CurrentRoundLabel(round),
            CurrentMonthLabel(round, isTournament),
            round?.StartDateUtc,
            league.MemberCount,
            stats?.OverallRank,
            MonthSlotRank(stats, isTournament),
            RoundRank(round, stats?.LiveRoundRank),
            stats?.SnapshotOverallRank,
            PreRoundMonthSlotRank(stats, isTournament),
            RoundRank(round, stats?.StableRoundRank),
            round?.Status.ToString(),
            round?.InProgressMatchCount ?? 0,
            round?.CompletedMatchCount ?? 0,
            league.UserWinnings,
            PrizeFund.Remaining(totalPrizeFund, league.TotalPaidOut),
            totalPrizeFund,
            league.Price,
            league.IsFree,
            CountWins(completedScores, score => score.Round.RoundId, userId),
            CountWins(completedScores, score => MonthOf(score.Round), userId),
            SeasonCompletion.IsFinished(league.CompletedRoundCount, league.NumberOfRounds),
            league.IsArchivedByUser,
            StageNameOf(round),
            stats?.StageRank,
            stats?.PreRoundStageRank);
    }

    /// <summary>
    /// The round's label. Always "Round N", even where the round has been given a name of its own.
    /// </summary>
    /// <remarks>
    /// Preserved from the old <c>'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10))</c> rather than switched to
    /// <c>Round.GetDisplayNameOrDefault</c>, which every other part of the site uses and which would show "Semi
    /// Finals" here instead. Changing it would change what a live tile says, so it is a question for the owner
    /// rather than a refactor - see the plan document.
    /// </remarks>
    private static string? CurrentRoundLabel(MyLeagueRoundRow? round) =>
        round is null ? null : $"Round {round.RoundNumber}";

    /// <summary>
    /// What the tile's second slot is called: nothing before a season starts, the month the round began in for a
    /// league, and "Exact Scores" for a tournament, which ranks on exact scores rather than by month.
    /// </summary>
    private static string? CurrentMonthLabel(MyLeagueRoundRow? round, bool isTournament)
    {
        if (round is null)
            return null;

        if (isTournament)
            return "Exact Scores";

        return MonthName.Of(round.StartDateUtc.Month);
    }

    /// <summary>
    /// The rank shown in that second slot, taken from whichever cached column the competition uses. The cache holds
    /// both metrics under their own names, and this is the only place the swap happens - so a tournament can never
    /// show a points rank where it promises exact scores.
    /// </summary>
    private static int? MonthSlotRank(MyLeagueStatsRow? stats, bool isTournament) =>
        isTournament ? stats?.ExactScoresRank : stats?.MonthRank;

    private static int? PreRoundMonthSlotRank(MyLeagueStatsRow? stats, bool isTournament) =>
        isTournament ? stats?.PreRoundExactScoresRank : stats?.SnapshotMonthRank;

    /// <summary>
    /// A round's rank. Before a round starts nobody has scored in it, so everyone is joint first rather than
    /// unranked - which is what the old <c>CASE WHEN ar.[Status] = @PublishedStatus THEN 1</c> said, and it applies
    /// to the live rank and the stable rank alike.
    /// </summary>
    private static int? RoundRank(MyLeagueRoundRow? round, int? cachedRank) =>
        round?.Status == RoundStatus.Published ? 1 : cachedRank;

    /// <summary>
    /// The stage the active round belongs to, or nothing when the season has no stage mapping for it.
    /// </summary>
    /// <remarks>
    /// A round with no mapping row shows no stage at all, which is not the same as a mapped round that is not a
    /// group round - that one is a knockout. The old query got this from a <c>LEFT JOIN</c> returning null, with the
    /// classification itself as <c>CASE WHEN trm.[Stages] LIKE '%Group%'</c>. The classification is now
    /// <c>TournamentStageClassifier</c> and the wording <c>TournamentStageName</c>.
    /// </remarks>
    private static string? StageNameOf(MyLeagueRoundRow? round)
    {
        if (round?.Stages is null)
            return null;

        return TournamentStageName.For(TournamentStageClassifier.ClassifyFrom(round.Stages));
    }

    private static int CountWins<TPeriod>(
        IReadOnlyList<ScoredRound> completedScores,
        Func<ScoredRound, TPeriod> periodSelector,
        string userId)
        where TPeriod : notnull =>
        Wins.ByPeriod(completedScores, periodSelector, score => score.UserId, score => score.Points)
            .Count(winnerId => winnerId == userId);

    /// <summary>
    /// Every member's scores in the league's completed rounds, paired with the round they came from - the raw
    /// material for the rounds-won and months-won counts.
    /// </summary>
    private static IReadOnlyList<ScoredRound> CompletedScores(
        LeagueTile tile,
        IEnumerable<MyLeagueRoundScoreRow> leagueScores)
    {
        var completedRoundsById = tile.SeasonRounds
            .Where(round => round.Status == RoundStatus.Completed)
            .ToDictionary(round => round.RoundId);

        return leagueScores
            .Where(score => completedRoundsById.ContainsKey(score.RoundId))
            .Select(score => new ScoredRound(score.UserId, completedRoundsById[score.RoundId], score.BoostedPoints))
            .ToList();
    }

    private static (int Year, int Month) MonthOf(MyLeagueRoundRow round) =>
        (round.StartDateUtc.Year, round.StartDateUtc.Month);

    private sealed record ScoredRound(string UserId, MyLeagueRoundRow Round, int Points);

    /// <summary>One league's card, once its active round and cached ranks have been found.</summary>
    private sealed record LeagueTile(
        MyLeagueRow League,
        MyLeagueRoundRow? ActiveRound,
        MyLeagueStatsRow? Stats,
        IReadOnlyList<MyLeagueRoundRow> SeasonRounds) : ILeagueTile
    {
        public bool HasRoundInProgress => ActiveRound?.Status == RoundStatus.InProgress;

        public DateTime SeasonStartDateUtc => League.SeasonStartDateUtc;

        public decimal Price => League.Price;

        public string LeagueName => League.LeagueName;
    }
}
