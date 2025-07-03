using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Leagues
{
    public class UpdateLeagueRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;
        public string? EntryCode { get; set; }
    }
}
