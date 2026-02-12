namespace PredictionLeague.Domain.Services.Boosts;

public sealed class BoostWindowSnapshot
{
    public int StartRoundNumber { get; init; }
    public int EndRoundNumber { get; init; }
    public int MaxUsesInWindow { get; init; }
}