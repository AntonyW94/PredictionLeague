using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One approved membership of a league in this round's season, with the two cached positions.</summary>
/// <remarks>
/// Both ranks come from the cache the write path maintains under ADR-0015 rather than being computed here. What the
/// digest does with them - the number of places moved, and the arrow that follows from it - is a rule, and the old
/// statement did that subtraction in a <c>CASE</c>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundDigestMembershipRow(
    string UserId,
    int LeagueId,
    string LeagueName,
    int? OverallRank,
    int? SnapshotOverallRank);
