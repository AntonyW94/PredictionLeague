namespace PredictionLeague.Contracts.Admin.Matches;

public class BaseMatchRequest
{
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime MatchDateTime { get; set; }
}