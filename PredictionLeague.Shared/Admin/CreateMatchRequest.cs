using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin;

public class CreateMatchRequest
{
    [Required]
    public int HomeTeamId { get; set; }

    [Required]
    public int AwayTeamId { get; set; }

    [Required]
    public DateTime MatchDateTime { get; set; }
}