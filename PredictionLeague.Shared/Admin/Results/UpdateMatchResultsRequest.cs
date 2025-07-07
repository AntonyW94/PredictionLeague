using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Results;

public class UpdateMatchResultsRequest
{
    [Required]
    public int MatchId { get; init; }
    
    [Required]
    [Range(0, 9)]
    public int HomeScore { get; init; }
    
    [Required]
    [Range(0, 9)]
    public int AwayScore { get; init; }
}