namespace PredictionLeague.Contracts.Predictions;

public class SubmitPredictionsRequest
{
    public int RoundId { get; set; }
    public List<PredictionSubmissionDto> Predictions { get; set; } = new();
}