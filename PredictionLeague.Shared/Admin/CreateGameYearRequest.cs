using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin
{
    public class CreateGameYearRequest
    {
        [Required]
        [StringLength(50, MinimumLength = 4)]
        public string YearName { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
    }
}