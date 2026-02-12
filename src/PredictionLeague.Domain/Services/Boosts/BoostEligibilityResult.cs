namespace PredictionLeague.Domain.Services.Boosts;

public sealed class BoostEligibilityResult
{
    public bool CanUse { get; }
    public string? Reason { get; }
    public int RemainingSeasonUses { get; }
    public int RemainingWindowUses { get; }
    public bool AlreadyUsedThisRound { get; }

    private BoostEligibilityResult(
        bool canUse,
        string? reason,
        int remainingSeasonUses,
        int remainingWindowUses,
        bool alreadyUsedThisRound)
    {
        CanUse = canUse;
        Reason = reason;
        RemainingSeasonUses = remainingSeasonUses;
        RemainingWindowUses = remainingWindowUses;
        AlreadyUsedThisRound = alreadyUsedThisRound;
    }

    public static BoostEligibilityResult NotAllowed(string reason) =>
        new(
            canUse: false,
            reason: reason,
            remainingSeasonUses: 0,
            remainingWindowUses: 0,
            alreadyUsedThisRound: false);

    public static BoostEligibilityResult AlreadyUsedThisRoundResult() =>
        new(
            canUse: false,
            reason: "Boost already used for this league and round.",
            remainingSeasonUses: 0,
            remainingWindowUses: 0,
            alreadyUsedThisRound: true);

    public static BoostEligibilityResult Allowed(int seasonRemaining, int windowRemaining) =>
        new(
            canUse: true,
            reason: null,
            remainingSeasonUses: seasonRemaining,
            remainingWindowUses: windowRemaining,
            alreadyUsedThisRound: false);
}