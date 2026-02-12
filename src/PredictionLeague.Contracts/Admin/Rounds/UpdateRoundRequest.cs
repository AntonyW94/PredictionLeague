using PredictionLeague.Contracts.Admin.Matches;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class UpdateRoundRequest
{
    public int RoundNumber { get; set; }
    public string ApiRoundName { get; set; } = "";
    public DateTime StartDateUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public RoundStatus Status { get; set; }
    public List<UpdateMatchRequest> Matches { get; init; } = [];
}