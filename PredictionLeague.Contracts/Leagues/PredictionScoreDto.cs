namespace PredictionLeague.Contracts.Leagues;

public record PredictionScoreDto(
    int MatchId,
    int? HomeScore,
    int? AwayScore,
    int? PointsAwarded,
    bool IsHidden);