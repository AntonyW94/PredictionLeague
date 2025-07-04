namespace PredictionLeague.Core.Models;

public class Team
{
    public int Id { get; init; }
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
}