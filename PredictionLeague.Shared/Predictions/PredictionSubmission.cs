using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Predictions;

public class PredictionSubmission
{
    [Required]
    public int MatchId { get; set; }

    [Range(0, 99)]
    public int PredictedHomeScore { get; set; }

    [Range(0, 99)]
    public int PredictedAwayScore { get; set; }
}