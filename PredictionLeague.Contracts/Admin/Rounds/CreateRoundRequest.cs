using PredictionLeague.Contracts.Admin.Matches;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class CreateRoundRequest
{
    public int SeasonId { get; init; }
    public int RoundNumber { get; set; }
    public string ApiRoundName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public List<CreateMatchRequest> Matches { get; init; } = [];
}