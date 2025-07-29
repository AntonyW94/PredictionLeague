using PredictionLeague.Contracts.Admin.Matches;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Admin.Rounds;

public class UpdateRoundRequest
{
    public int RoundNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime Deadline { get; set; }
    public RoundStatus Status { get; set; }
    public List<UpdateMatchRequest> Matches { get; init; } = new();
}