using System.ComponentModel.DataAnnotations;

namespace PredictionLeague.Shared.Admin.Teams;

public class CreateTeamRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Url]
    public string LogoUrl { get; set; } = string.Empty;
}