using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class UpdateRoundRequest
{
    public int RoundNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public List<UpdateMatchRequest> Matches { get; init; } = new();
}