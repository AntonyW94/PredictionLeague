using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Rounds;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record MatchInRoundDto(
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
    string? PlaceholderHomeName = null,
    string? PlaceholderAwayName = null,
    DateTime? CustomLockTimeUtc = null
);
