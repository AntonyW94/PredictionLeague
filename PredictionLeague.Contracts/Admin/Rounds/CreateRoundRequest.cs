using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class CreateRoundRequest
{
    public int SeasonId { get; set; }
    public int RoundNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public List<CreateMatchRequest> Matches { get; } = new();
}