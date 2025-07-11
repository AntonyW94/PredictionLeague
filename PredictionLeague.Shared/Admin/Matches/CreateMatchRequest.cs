namespace PredictionLeague.Shared.Admin.Matches;

public class CreateMatchRequest
{
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime MatchDateTime { get; set; }
}