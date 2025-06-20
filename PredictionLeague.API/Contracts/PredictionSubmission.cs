using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.API.Contracts;

public class PredictionSubmission
{
    [Required]
    public int MatchId { get; set; }

    [Range(0, 99)]
    public int PredictedHomeScore { get; set; }

    [Range(0, 99)]
    public int PredictedAwayScore { get; set; }
}