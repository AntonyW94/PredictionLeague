using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Leagues;

public class JoinLeagueRequest
{
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string EntryCode { get; set; } = string.Empty;
}