using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin;

public class CreateRoundRequest
{
    [Required]
    public int SeasonId { get; set; }

    [Required]
    [Range(1, 52)]
    public int RoundNumber { get; set; }

    [Required]
    public DateTime StartDate { get; set; }
        
    [Required]
    public DateTime Deadline { get; set; }

    public List<CreateMatchRequest> Matches { get; } = new();
}