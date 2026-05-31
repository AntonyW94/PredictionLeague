namespace ThePredictions.Contracts.Admin.Competitions;

public class BaseCompetitionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public int? ApiLeagueId { get; set; }
}
