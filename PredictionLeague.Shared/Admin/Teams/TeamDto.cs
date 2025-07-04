namespace PredictionLeague.Shared.Admin.Teams;

public class TeamDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; } = string.Empty;
}