namespace PredictionLeague.Shared.Admin.Matches;

public class MatchDto
{
    public int Id { get; init; }
    public int HomeTeamId { get; init; }
    public string HomeTeamName { get; init; } = string.Empty;
    public string HomeTeamLogoUrl { get; init; } = string.Empty;
    public int AwayTeamId { get; init; }
    public string AwayTeamName { get; init; } = string.Empty;
    public string AwayTeamLogoUrl { get; init; } = string.Empty;
    public DateTime MatchDateTime { get; init; }
    public int? ActualHomeScore { get; init; }
    public int? ActualAwayScore { get; init; }
}