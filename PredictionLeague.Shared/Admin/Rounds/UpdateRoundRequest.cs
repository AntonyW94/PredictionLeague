using PredictionLeague.Shared.Admin.Matches;

namespace PredictionLeague.Shared.Admin.Rounds;

public class UpdateRoundRequest
{
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public List<UpdateMatchRequest> Matches { get; init; } = new();
}