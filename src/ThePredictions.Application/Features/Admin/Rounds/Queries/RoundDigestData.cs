using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>What <see cref="IRoundDigestQuery"/> returns.</summary>
/// <remarks>
/// <see cref="SeasonRounds"/> holds every round of the season the requested round belongs to, including that round
/// itself, and is empty when there is no such round. Which one comes next is a rule, so the read cannot pick it - and
/// the old statement's <c>TOP 1 ... ORDER BY RoundNumber</c> was that rule in SQL.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundDigestData(
    IReadOnlyList<RoundDigestRoundRow> SeasonRounds,
    IReadOnlyList<RoundDigestPlayerRow> Players,
    IReadOnlyList<RoundDigestMembershipRow> Memberships,
    IReadOnlyList<RoundLeagueScoreRow> LeagueScores);
