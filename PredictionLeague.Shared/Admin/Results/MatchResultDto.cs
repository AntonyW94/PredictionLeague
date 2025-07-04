namespace PredictionLeague.Shared.Admin.Results;

public class MatchResultDto
{
    public int Id { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public string HomeTeamLogoUrl { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string AwayTeamLogoUrl { get; set; } = string.Empty;
    public int? ActualHomeScore { get; set; }
    public int? ActualAwayScore { get; set; }
}