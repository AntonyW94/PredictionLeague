using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public record RoundDto(
    int Id,
    int SeasonId,
    int RoundNumber,
    DateTime StartDate,
    DateTime Deadline,
    RoundStatus Status,
    int MatchCount);