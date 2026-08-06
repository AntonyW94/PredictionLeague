using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Matches;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class BaseMatchRequest
{
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public DateTime MatchDateTimeUtc { get; set; }
    public int? ExternalId { get; set; }
}
