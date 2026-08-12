using MediatR;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
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

        var isTournament = league.CompetitionType == CompetitionType.Tournament;

        var totalPrizeFund = PrizeFund.Total(league.Price, league.MemberCount, league.PrizeFundOverride);

        var completedScores = CompletedScores(tile, leagueScores);

        var round = RoundFactsOf(tile.ActiveRound, isTournament);
        var ranks = RanksOf(tile.Stats, tile.ActiveRound, isTournament);

        return new MyLeagueDto(
            league.LeagueId,
            league.LeagueName,
            league.SeasonName,
            league.CompetitionType,
            league.SeasonStartDateUtc,
            league.EntryDeadlineUtc,
            round.Label,
            round.MonthLabel,
            round.StartDateUtc,
            league.MemberCount,
            ranks.Overall,
            ranks.MonthSlot,
            ranks.LiveRound,
            ranks.PreRoundOverall,
            ranks.PreRoundMonthSlot,
            ranks.StableRound,
            round.Status,
            round.InProgressMatchCount,
            round.CompletedMatchCount,
            league.UserWinnings,
            PrizeFund.Remaining(totalPrizeFund, league.TotalPaidOut),
            totalPrizeFund,
            league.Price,
            league.IsFree,
            CountWins(completedScores, score => score.Round.RoundId, userId),
            CountWins(completedScores, score => MonthOf(score.Round), userId),
            SeasonCompletion.IsEveryRoundComplete(
                roundCount: league.SeasonRoundCount,
                completedRoundCount: league.CompletedRoundCount),
            league.IsArchivedByUser,
            round.StageName,
            ranks.Stage,
            ranks.PreRoundStage);
    }

    /// <summary>
    /// What the tile says about the round in play, or what it says when there is not one.
    /// </summary>
    /// <remarks>
    /// A league between rounds - or one whose season has not started - is the ordinary case, and every one of these
    /// fields had to answer for it separately where the reply was being built. Answered once here instead.
    /// </remarks>
    private static RoundFacts RoundFactsOf(MyLeagueRoundRow? round, bool isTournament) =>
        round is null
            ? RoundFacts.NoRoundInPlay
            : new RoundFacts(
                CurrentRoundLabel(round),
                CurrentMonthLabel(round, isTournament),
                round.StartDateUtc,
                round.Status.ToString(),
                round.InProgressMatchCount,
                round.CompletedMatchCount,
                StageNameOf(round));

    /// <summary>
    /// Every position on the tile, read from the cache per ADR-0015 rather than worked out here.
    /// </summary>
    /// <remarks>
    /// A player with no cached row has no positions - which happens between joining a league and the next time the
    /// ranks are written. The round ranks are the exception: before a round starts nobody has scored in it, so
    /// everyone is joint first whether or not there is a cached row to say so.
    /// </remarks>
    private static TileRanks RanksOf(MyLeagueStatsRow? stats, MyLeagueRoundRow? round, bool isTournament)
    {
        if (stats is null)
            return TileRanks.Unranked with
            {
                LiveRound = RoundRank(round, null),
                StableRound = RoundRank(round, null)
            };

        return new TileRanks(
            stats.OverallRank,
            MonthSlotRank(stats, isTournament),
            RoundRank(round, stats.LiveRoundRank),
            stats.SnapshotOverallRank,
            PreRoundMonthSlotRank(stats, isTournament),
            RoundRank(round, stats.StableRoundRank),
            stats.StageRank,
            stats.PreRoundStageRank);
    }

    /// <summary>
    /// The round's name: the one an administrator gave it, or "Round N" where nobody has.
    /// </summary>
    /// <remarks>
    /// The same rule as every other screen, which is the point - this tile was the last place naming a round by number
    /// regardless, from the old <c>'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10))</c>. A round called "Semi Finals"
    /// showed as "Round 12" here and as "Semi Finals" everywhere else.
    ///
    /// There being no round in play is <see cref="RoundFacts.NoRoundInPlay"/>'s answer, not this one's.
    /// </remarks>
    private static string CurrentRoundLabel(MyLeagueRoundRow round) =>
        Round.DisplayNameOrDefault(round.DisplayName, round.RoundNumber);

    /// <summary>
    /// What the tile's second slot is called: the month the round began in for a league, and "Exact Scores" for a
    /// tournament, which ranks on exact scores rather than by month.
    /// </summary>
    private static string? CurrentMonthLabel(MyLeagueRoundRow round, bool isTournament)
    {
        if (isTournament)
            return "Exact Scores";

        return MonthName.Of(round.StartDateUtc.Month);
    }

    /// <summary>
    /// The rank shown in that second slot, taken from whichever cached column the competition uses. The cache holds
    /// both metrics under their own names, and this is the only place the swap happens - so a tournament can never
    /// show a points rank where it promises exact scores.
    /// </summary>
    private static int? MonthSlotRank(MyLeagueStatsRow stats, bool isTournament) =>
        isTournament ? stats.ExactScoresRank : stats.MonthRank;

    private static int? PreRoundMonthSlotRank(MyLeagueStatsRow stats, bool isTournament) =>
        isTournament ? stats.PreRoundExactScoresRank : stats.SnapshotMonthRank;

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
    private static string? StageNameOf(MyLeagueRoundRow round)
    {
        if (round.Stages is null)
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

    /// <summary>What the tile shows about the round in play.</summary>
    private sealed record RoundFacts(
        string? Label,
        string? MonthLabel,
        DateTime? StartDateUtc,
        string? Status,
        int InProgressMatchCount,
        int CompletedMatchCount,
        string? StageName)
    {
        /// <summary>No round in play: no labels, no dates, and no fixtures under way or finished in one.</summary>
        internal static readonly RoundFacts NoRoundInPlay = new(null, null, null, null, 0, 0, null);
    }

    /// <summary>Where the player stands in each of the tile's four races, now and before the round began.</summary>
    private sealed record TileRanks(
        int? Overall,
        int? MonthSlot,
        int? LiveRound,
        int? PreRoundOverall,
        int? PreRoundMonthSlot,
        int? StableRound,
        int? Stage,
        int? PreRoundStage)
    {
        /// <summary>No cached row for this player in this league, so no position in any of them.</summary>
        internal static readonly TileRanks Unranked = new(null, null, null, null, null, null, null, null);
    }

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
