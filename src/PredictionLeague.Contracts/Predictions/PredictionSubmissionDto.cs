namespace PredictionLeague.Contracts.Predictions;

public record PredictionSubmissionDto(
    int MatchId,
    int HomeScore,
    int AwayScore
);