namespace ThePredictions.Contracts.SeasonPasses;

public record PastSeasonPassDto(
    int SeasonId,
    string SeasonName,
    int PlayerCount);
