using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public record MatchInRoundDto(
    int Id,
    DateTime MatchDateTime,
    int HomeTeamId,
    string HomeTeamName,
    string? HomeTeamLogoUrl,
    int AwayTeamId,
    string AwayTeamName,
    string? AwayTeamLogoUrl,
    int? ActualHomeTeamScore,
    int? ActualAwayTeamScore,
    MatchStatus Status
);
