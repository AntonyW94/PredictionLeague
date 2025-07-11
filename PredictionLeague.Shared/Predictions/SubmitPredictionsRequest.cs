namespace PredictionLeague.Shared.Predictions;

public class SubmitPredictionsRequest
{
    public int RoundId { get; set; }
    public List<PredictionSubmission> Predictions { get; set; } = new();
}