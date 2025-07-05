using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Results;

public class UpdateResultRequest
{
    [Required]
    public int MatchId { get; init; }
    [Required]
    [Range(0, 99)]
    public int HomeScore { get; init; }
    [Required]
    [Range(0, 99)]
    public int AwayScore { get; init; }
}