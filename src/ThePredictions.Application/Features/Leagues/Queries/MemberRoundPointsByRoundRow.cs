using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One member's points for one round, identified by round.
/// </summary>
/// <remarks>
/// Distinct from <see cref="MemberRoundPointsRow"/>, which is already scoped to the rounds that matter and so
/// needs no round id. Here the caller has to know which round each row belongs to, because it filters by
/// tournament stage and then excludes the round currently in progress.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MemberRoundPointsByRoundRow(string UserId, int RoundId, int BoostedPoints);
