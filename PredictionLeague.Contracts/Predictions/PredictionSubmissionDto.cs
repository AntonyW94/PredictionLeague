namespace PredictionLeague.Contracts.Predictions;

public class PredictionSubmissionDto
{
    public int MatchId { get; init; }
    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }
}