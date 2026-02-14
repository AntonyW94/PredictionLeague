using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Domain.Services.Boosts;

[ExcludeFromCodeCoverage]
public sealed class BoostUsageSnapshot
{
    public int SeasonUses { get; init; }
    public int WindowUses { get; init; }
    public bool HasUsedThisRound { get; init; }
}