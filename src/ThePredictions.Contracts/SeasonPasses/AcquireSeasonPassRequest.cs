using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.SeasonPasses;

[ExcludeFromCodeCoverage]
public class AcquireSeasonPassRequest
{
    public int SeasonId { get; set; }
}
