using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Boosts;

/// <summary>An optional usage window for a boost: caps uses within a round range.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class BoostWindowSelectionDto
{
    public int StartRoundNumber { get; set; }
    public int EndRoundNumber { get; set; }
    public int MaxUsesInWindow { get; set; }
}
