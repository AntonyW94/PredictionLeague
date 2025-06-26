using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Predictions;

public class SubmitPredictionsRequest
{
    [Required]
    public int RoundId { get; set; }

    [Required]
    [MinLength(1)]
    public List<PredictionSubmission> Predictions { get; set; } = new();
}