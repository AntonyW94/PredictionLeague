using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Predictions;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class SubmitPredictionsRequest
{
    public int RoundId { get; set; }
    public List<PredictionSubmissionDto> Predictions { get; set; } = [];
}
