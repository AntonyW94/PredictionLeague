using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Contracts.Boosts;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
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