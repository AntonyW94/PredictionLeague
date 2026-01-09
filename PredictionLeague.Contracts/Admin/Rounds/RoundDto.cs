using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public record RoundDto(
    int Id,
    int SeasonId,
    int RoundNumber,
    string? ApiRoundName,
    DateTime StartDateUtc,
    DateTime DeadlineUtc,
    RoundStatus Status,
    int MatchCount);