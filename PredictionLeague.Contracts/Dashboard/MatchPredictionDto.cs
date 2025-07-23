namespace PredictionLeague.Contracts.Dashboard;

public class MatchPredictionDto
{
    public int MatchId { get; init; }
    public DateTime MatchDateTime { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string? HomeTeamLogoUrl { get; init; } = string.Empty;
    public string AwayTeamName { get; init; } = string.Empty;
    public string? AwayTeamLogoUrl { get; init; } = string.Empty;
    public int? PredictedHomeScore { get; set; }
    public int? PredictedAwayScore { get; set; }
}