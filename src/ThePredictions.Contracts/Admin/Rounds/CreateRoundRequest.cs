using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Admin.Matches;

namespace ThePredictions.Contracts.Admin.Rounds;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class CreateRoundRequest
{
    public int SeasonId { get; init; }
    public int RoundNumber { get; set; }
    public string ApiRoundName { get; set; } = string.Empty;
    public DateTime StartDateUtc { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public List<CreateMatchRequest> Matches { get; init; } = [];
}
