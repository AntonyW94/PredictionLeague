using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Leagues;

public class JoinLeagueRequest
{
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public static string EntryCode => string.Empty;
}