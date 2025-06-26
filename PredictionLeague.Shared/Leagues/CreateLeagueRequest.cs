using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Leagues
{
    public class CreateLeagueRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int GameYearId { get; set; }
    }
}
