using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Services.Boosts;

[ExcludeFromCodeCoverage]
public sealed class BoostWindowSnapshot
{
    public int StartRoundNumber { get; init; }
    public int EndRoundNumber { get; init; }
    public int MaxUsesInWindow { get; init; }
}