using PredictionLeague.Shared.Admin.Matches;
using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Rounds
{
    public class UpdateRoundRequest
    {
        [Required]
        public DateTime StartDate { get; set; }
        [Required]
        public DateTime Deadline { get; set; }
        public List<UpdateMatchRequest> Matches { get; set; } = new();
    }
}
