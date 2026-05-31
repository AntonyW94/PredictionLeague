namespace ThePredictions.Contracts.SeasonPasses;

public record PastSeasonPassDto(
    int SeasonId,
    string SeasonName,
    string? CompetitionLogoUrl,
    int PlayerCount);
