namespace PredictionLeague.Core.Models;

public class Match
{
    public int Id { get; init; }
    public int RoundId { get; init; }
    public int HomeTeamId { get; init; }
    public int AwayTeamId { get; init; }
    public DateTime MatchDateTime { get; init; }
    public MatchStatus Status { get; set; }
    public int? ActualHomeTeamScore { get; set; }
    public int? ActualAwayTeamScore { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
}