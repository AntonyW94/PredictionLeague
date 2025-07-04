using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Results;

public class UpdateResultRequest
{
    [Required]
    public int MatchId { get; set; }
    [Required]
    [Range(0, 99)]
    public int HomeScore { get; set; }
    [Required]
    [Range(0, 99)]
    public int AwayScore { get; set; }
}