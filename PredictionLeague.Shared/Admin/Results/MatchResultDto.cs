namespace PredictionLeague.Shared.Admin.Results;

public class MatchResultDto
{
    public int Id { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeTeamLogoUrl { get; init; } = string.Empty;
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayTeamLogoUrl { get; init; } = string.Empty;
    public int? ActualHomeScore { get; set; }
    public int? ActualAwayScore { get; set; }
}