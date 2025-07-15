namespace PredictionLeague.Contracts.Admin.Teams;

public class TeamDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? LogoUrl { get; init; } = string.Empty;
}