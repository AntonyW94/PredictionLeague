using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Domain.Services.Boosts;

[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed class BoostWindowSnapshot
{
    public int StartRoundNumber { get; init; }
    public int EndRoundNumber { get; init; }
    public int MaxUsesInWindow { get; init; }
}
