using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage]
public class SubmitPredictionsRequest
{
    public int RoundId { get; set; }
    public List<PredictionSubmissionDto> Predictions { get; set; } = [];
}
