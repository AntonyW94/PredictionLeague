namespace PredictionLeague.Shared.Admin.Results;

public class UpdateMatchResultsRequest
{
    public int MatchId { get; init; }
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
}