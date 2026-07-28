using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Homepage;

public record HomepageSeasonDto(
    int Id,
    string Name,
    CompetitionType CompetitionType,
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    bool IsInProgress,
    bool IsUpcoming,
    int LeagueCount,
    int PlayerCount,
    decimal TotalPrizeFund
);
