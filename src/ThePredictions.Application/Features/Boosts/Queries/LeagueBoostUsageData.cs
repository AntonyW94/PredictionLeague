using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// Everything the boost-usage page needs from the database, in one reply.
///
/// One composite rather than eight separate port methods, because <b>how</b> to fetch it is a persistence
/// decision: the SQL Server adapter runs several reads concurrently, and another adapter is free to batch
/// them, join them, or serve them from one statement. The handler should not be expressing a fetch strategy.
///
/// <see cref="Usages"/> is uncensored. The rule about which of another player's boosts may be seen is
/// applied by <see cref="BoostUsageVisibility"/> against an injected clock.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueBoostUsageData(
    int SeasonId,
    IReadOnlyList<BoostRuleRow> BoostRules,
    IReadOnlyList<BoostWindowRow> Windows,
    IReadOnlyList<BoostMemberRow> Members,
    IReadOnlyList<BoostUsageRow> Usages,
    BoostRoundRangeRow? RoundRange,
    int? InProgressRoundNumber,
    int? LastCompletedRoundNumber);
