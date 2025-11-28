namespace PredictionLeague.Domain.Services.Boosts;

public sealed class BoostUsageSnapshot
{
    public int SeasonUses { get; init; }
    public int WindowUses { get; init; }
    public bool HasUsedThisRound { get; init; }
}