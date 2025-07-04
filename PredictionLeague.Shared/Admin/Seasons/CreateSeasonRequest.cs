using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Seasons;

public class CreateSeasonRequest
{
    [Required]
    [StringLength(50, MinimumLength = 4)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}