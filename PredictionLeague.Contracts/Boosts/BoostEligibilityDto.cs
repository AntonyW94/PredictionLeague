namespace PredictionLeague.Contracts.Boosts;

public sealed class BoostEligibilityDto
{
    public string BoostCode { get; init; } = string.Empty;
    public int LeagueId { get; init; }
    public int RoundId { get; init; }
    public bool CanUse { get; init; }
    public string? Reason { get; init; }
    public int RemainingSeasonUses { get; init; }
    public int RemainingWindowUses { get; init; }
    public bool AlreadyUsedThisRound { get; init; }
}