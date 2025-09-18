using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public record RoundWithAllPredictionsInDto(
    int Id,
    int SeasonId,
    int RoundNumber,
    string ApiRoundName,
    DateTime StartDate,
    DateTime Deadline,
    RoundStatus Status,
    int MatchCount,
    bool AllPredictionsIn);