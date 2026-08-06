using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class BoostUsageSnapshot
{
    public int SeasonUses { get; init; }
    public int WindowUses { get; init; }
    public bool HasUsedThisRound { get; init; }
}
