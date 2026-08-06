using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class AcquireSeasonPassRequest
{
    public int SeasonId { get; set; }
}
