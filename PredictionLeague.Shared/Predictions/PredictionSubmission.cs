using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Predictions;

public class PredictionSubmission
{
    [Required]
    public int MatchId { get; init; }

    [Range(0, 9)]
    public int PredictedHomeScore { get; init; }

    [Range(0, 9)]
    public int PredictedAwayScore { get; init; }
}