using System.Diagnostics.CodeAnalysis;
namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Flat Dapper row for the round-results digest: one row per (user, league). User-level and
/// next-round fields repeat across a user's league rows and are collapsed when grouped into
/// <see cref="UserRoundDigest"/>.
/// </summary>
/// <remarks>
/// SELECT column order in <c>GetRoundDigestQueryHandler</c> must match this constructor exactly
/// (Dapper maps positionally by name and type).
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
public record RoundDigestRow(
    string UserId,
    string Email,
    string FirstName,
    string RoundName,
    int ExactScoreCount,
    int CorrectResultCount,
    int LeagueId,
    string LeagueName,
    int LeaguePoints,
    int? Position,
    int? PositionDelta,
    string? TopScorerName,
    int? TopScorerPoints,
    string? NextRoundName,
    DateTime? NextRoundDeadlineUtc);
