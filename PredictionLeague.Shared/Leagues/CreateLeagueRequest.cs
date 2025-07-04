using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Leagues;

public class CreateLeagueRequest
{
    [Required]
    public int SeasonId { get; set; }
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;
    public string? EntryCode { get; set; }
}