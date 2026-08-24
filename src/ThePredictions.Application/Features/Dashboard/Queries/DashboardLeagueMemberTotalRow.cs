using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One member's points total in one league, across every round they have scored in.
/// </summary>
/// <remarks>
/// Not <c>MemberRoundPointsRow</c> for the same reason as <see cref="DashboardLeagueMemberRow"/>: the tile shows
/// several leagues at once, so which league a row belongs to cannot be implied by the caller.
///
/// Totalled by the database rather than in the handler, unlike almost everything else this tile reads. The tile has
/// no use for a single round's points - it needs the sum and nothing else - and the unaggregated form meant every
/// poll shipped one row per (member, round) for every season the player has ever entered. On production data that
/// was the whole table. A sum over a column is arithmetic rather than a rule, the same category as the round counts
/// in <see cref="DashboardLeagueRow"/>: no tie is broken, no order is chosen and no threshold is applied, so nothing
/// the tile decides has moved out of C#.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record DashboardLeagueMemberTotalRow(int LeagueId, string UserId, int TotalPoints);
