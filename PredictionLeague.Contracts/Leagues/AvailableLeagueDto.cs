namespace PredictionLeague.Contracts.Leagues;

public record AvailableLeagueDto(
    int Id,
    string Name,
    string SeasonName,
    decimal Price,
    DateTime EntryDeadline);