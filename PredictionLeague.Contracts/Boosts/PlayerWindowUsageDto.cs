namespace PredictionLeague.Contracts.Boosts;

public sealed class PlayerWindowUsageDto
{
    public string UserId { get; init; } = string.Empty;
    public string PlayerName { get; init; } = string.Empty;
    public int Remaining { get; init; }
    public int MaxUses { get; init; }
    public bool IsCurrentUser { get; init; }
    public List<int> RoundsUsedIn { get; init; } = [];
}
