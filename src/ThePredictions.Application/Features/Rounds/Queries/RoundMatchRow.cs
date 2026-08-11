using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>One fixture in a round, with both teams and whatever has happened to it.</summary>
/// <remarks>
/// The team columns are all nullable, and that is not defensive: a tournament fixture whose teams are not known yet
/// holds a placeholder name instead of two team ids, so the joins find nothing and every one of these is null. One of
/// the two statements this replaces declared them as never-null, which Dapper honours by putting a null in them anyway.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundMatchRow(
    int Id,
    DateTime MatchDateTimeUtc,
    int? MatchNumber,
    int? HomeTeamId,
    string? HomeTeamName,
    string? HomeTeamShortName,
    string? HomeTeamAbbreviation,
    string? HomeTeamLogoUrl,
    int? AwayTeamId,
    string? AwayTeamName,
    string? AwayTeamShortName,
    string? AwayTeamAbbreviation,
    string? AwayTeamLogoUrl,
    int? ActualHomeTeamScore,
    int? ActualAwayTeamScore,
    MatchStatus Status,
    string? PlaceholderHomeName,
    string? PlaceholderAwayName,
    DateTime? CustomLockTimeUtc);
