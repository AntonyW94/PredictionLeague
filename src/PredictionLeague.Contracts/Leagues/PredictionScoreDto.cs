using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Leagues;

public record PredictionScoreDto(
    int MatchId,
    int? HomeScore,
    int? AwayScore,
    PredictionOutcome Outcome,
    bool IsHidden);