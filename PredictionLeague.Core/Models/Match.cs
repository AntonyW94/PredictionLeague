namespace PredictionLeague.Core.Models;

public class Match
{
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime MatchDateTime { get; set; }
    public MatchStatus Status { get; set; }
    public int? ActualHomeTeamScore { get; set; }
    public int? ActualAwayTeamScore { get; set; }
    public Team? HomeTeam { get; set; }
    public Team? AwayTeam { get; set; }
}