using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The player's cached ranks in one league, read straight from <c>LeagueMemberStats</c>.
/// </summary>
/// <remarks>
/// Every one of these is maintained on the write path by <c>LeagueStatsRepository</c> under ADR-0015, because
/// computing them live cost roughly 400ms of query planning per dashboard load. Nothing here recomputes them.
///
/// A null rank is meaningful rather than missing: it says the position does not exist - no active round, or the
/// active round is the first of its season, month or stage so there is nothing to have moved from - and it is what
/// suppresses the change arrow on the tile.
///
/// The columns and this type are a contract with the writer. If what a rank means has to change, it changes in
/// <c>LeagueStatsRepository.RecomputeAsync</c>, not here, or the two silently disagree.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeagueStatsRow(
    int LeagueId,
    int? OverallRank,
    int? MonthRank,
    int? LiveRoundRank,
    int? SnapshotOverallRank,
    int? SnapshotMonthRank,
    int? StableRoundRank,
    int? StageRank,
    int? PreRoundStageRank,
    int? ExactScoresRank,
    int? PreRoundExactScoresRank);
