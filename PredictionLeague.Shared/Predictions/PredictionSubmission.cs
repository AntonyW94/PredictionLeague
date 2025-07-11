namespace PredictionLeague.Shared.Predictions;

public class PredictionSubmission
{
    public int MatchId { get; init; }
    public int PredictedHomeScore { get; init; }
    public int PredictedAwayScore { get; init; }
}