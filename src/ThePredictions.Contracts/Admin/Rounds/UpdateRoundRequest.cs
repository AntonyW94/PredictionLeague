using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Contracts.Admin.Rounds;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateRoundRequest
{
    public int RoundNumber { get; set; }
    public string ApiRoundName { get; set; } = "";
    public DateTime StartDateUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public RoundStatus Status { get; set; }
    public List<UpdateMatchRequest> Matches { get; init; } = [];
}
