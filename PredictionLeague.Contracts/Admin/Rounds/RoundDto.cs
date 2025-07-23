namespace PredictionLeague.Contracts.Admin.Rounds;

public record RoundDto(
    int Id,
    int SeasonId,
    int RoundNumber,
    DateTime StartDate,
    DateTime Deadline,
    int MatchCount);